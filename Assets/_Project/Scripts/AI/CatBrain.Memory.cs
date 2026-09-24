using System.Collections.Generic;
using RatGame.Core;
using RatGame.Noise;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>
    /// 장소 기억 + 앙심 (design/cat-ideas/05, 2026-09-24). 호스트 전용 — 런 안에서만 쌓인다(고양이가 새로 스폰되면 초기화).
    /// 장소 기억: 쥐를 본 자리·큰 소리가 난 자리를 2m 격자 열 지도로. 순찰 목적지를 고를 때 가끔 가장 뜨거운 칸에 가서 킁킁.
    /// 앙심: 추격에서 놓친 쥐·도발(찍찍)한 쥐에게. 임계 이상인 쥐 1명은 시야·청각 배율 + 우선 추적 + HUD "!!"·"찍힘".
    /// 귀속이 분명한 사건만 센다 — 충돌·파손 소음은 Source가 비어(0 = 환경, 호스트도 0) 제외.
    /// </summary>
    public partial class CatBrain
    {
        /// <summary>앙심 대상 (HasGrudge일 때만 유효). 클라 HUD "!!"·"찍힘" 칩용.</summary>
        public NetworkVariable<ulong> GrudgeClientId = new NetworkVariable<ulong>(0);
        public NetworkVariable<bool> HasGrudge = new NetworkVariable<bool>(false);
        /// <summary>기억 칸에서 냄새 맡는 중 (CatVisual 킁킁 모션).</summary>
        public NetworkVariable<bool> Sniffing = new NetworkVariable<bool>(false);

        private readonly Dictionary<Vector2Int, float> _heat = new();
        private readonly Dictionary<ulong, float> _grudge = new();
        private readonly List<Vector2Int> _heatKeys = new();
        private readonly List<ulong> _grudgeKeys = new();
        private bool _memoryVisit;        // 기억 칸으로 가는 중
        private Vector2Int _memoryCell;
        private float _sniffUntil2 = -1f; // -1 = 아직 도착 전
        private bool _lastPickWasMemory;  // 기억 칸 연속 방문 금지

        public float GetGrudge(ulong clientId) => _grudge.TryGetValue(clientId, out float g) ? g : 0f;
        public float GetHeatAt(Vector3 pos) => _heat.TryGetValue(Cell(pos), out float h) ? h : 0f;
        public bool IsMemoryVisit => _memoryVisit;

        private void InitMemory()
        {
            _senses.TargetGainMultiplier = GrudgeGain;
            _senses.Heard += OnHeardForMemory;
        }

        private float GrudgeGain(ulong clientId) => IsGrudged(clientId) ? _balance.CatGrudgeSightMul : 1f;
        private bool IsGrudged(ulong clientId) => HasGrudge.Value && GrudgeClientId.Value == clientId;

        private Vector2Int Cell(Vector3 p)
        {
            float s = Mathf.Max(0.5f, _balance.CatMemoryCellSize);
            return new Vector2Int(Mathf.FloorToInt(p.x / s), Mathf.FloorToInt(p.z / s));
        }

        private Vector3 CellCenter(Vector2Int c)
        {
            float s = Mathf.Max(0.5f, _balance.CatMemoryCellSize);
            return new Vector3((c.x + 0.5f) * s, transform.position.y, (c.y + 0.5f) * s);
        }

        private void AddHeat(Vector3 pos, float amount)
        {
            var c = Cell(pos);
            _heat.TryGetValue(c, out float h);
            _heat[c] = h + amount;
        }

        /// <summary>앙심 가산 (호스트). 테스트·다른 시스템(유인 속음 등)에서도 호출.</summary>
        public void ServerAddGrudge(ulong clientId, float amount, string why)
        {
            if (!IsServer) return;
            _grudge.TryGetValue(clientId, out float g);
            g = Mathf.Clamp(g + amount, 0f, 100f);
            _grudge[clientId] = g;
            Log.Dev($"고양이 [{name}]: 앙심 client {clientId} {g:0} ({why} +{amount:0})");
        }

        private void OnHeardForMemory(NoiseEvent e, float heard)
        {
            if (heard >= _balance.CatMemoryNoiseMin) AddHeat(e.Pos, heard / _balance.CatMemoryNoiseMin);
            if (e.Type == NoiseType.Squeak && State.Value != CatState.Sleep)
                ServerAddGrudge(e.Source, _balance.CatGrudgeSqueak, "찍찍 도발");
        }

        // Update 맨 앞에서 매 프레임 (호스트)
        private void TickMemory()
        {
            float dt = Time.deltaTime;
            if (_senses.VisibleTarget != null)
                AddHeat(_senses.VisibleTarget.transform.position, _balance.CatMemorySightPerSec * dt);

            // 감쇠 — 0이 되면 지운다
            _heatKeys.Clear(); _heatKeys.AddRange(_heat.Keys);
            float decay = _balance.CatMemoryDecayPerSec * dt;
            foreach (var k in _heatKeys)
            {
                float h = _heat[k] - decay;
                if (h <= 0f) _heat.Remove(k); else _heat[k] = h;
            }

            // 앙심 감쇠 + 대표 1명 (임계 이상 중 최고)
            _grudgeKeys.Clear(); _grudgeKeys.AddRange(_grudge.Keys);
            float gDecay = _balance.CatGrudgeDecayPerSec * dt;
            ulong top = 0; float topVal = -1f;
            foreach (var k in _grudgeKeys)
            {
                float g = Mathf.Max(0f, _grudge[k] - gDecay);
                _grudge[k] = g;
                if (g > topVal) { topVal = g; top = k; }
            }
            bool has = topVal >= _balance.CatGrudgeThreshold;
            if (HasGrudge.Value != has) { HasGrudge.Value = has; Log.Dev($"고양이 [{name}]: {(has ? $"client {top} 찍음" : "앙심 풀림")}"); }
            if (has && GrudgeClientId.Value != top) GrudgeClientId.Value = top;
        }

        // GoToNextSpot 맨 앞 — 확률로 가장 뜨거운 기억 칸에 들른다
        private bool TryGoToMemorySpot()
        {
            if (_lastPickWasMemory) { _lastPickWasMemory = false; return false; }
            if (Random.value >= _balance.CatMemoryPatrolChance) return false;
            float best = _balance.CatMemoryMinHeat;
            bool found = false;
            foreach (var kv in _heat)
                if (kv.Value >= best) { best = kv.Value; _memoryCell = kv.Key; found = true; }
            if (!found) return false;
            _memoryVisit = true;
            _lastPickWasMemory = true;
            _sniffUntil2 = -1f;
            Vector3 dest = CatMovement.Sample(CellCenter(_memoryCell), 1.5f, CellCenter(_memoryCell));
            _movement.MoveTo(dest, _balance.CatPatrolSpeed);
            Log.Dev($"고양이 [{name}]: 기억 칸 {_memoryCell} 확인하러 (열 {best:0.0})");
            return true;
        }

        // TickPatrol에서 기억 칸 방문 중이면 이것만
        private void TickMemoryVisit()
        {
            if (_sniffUntil2 < 0f)
            {
                if (!_movement.Arrived) return;
                _movement.Stop();
                _sniffUntil2 = Time.time + _balance.CatMemorySniffSeconds;
                Sniffing.Value = true;
                Log.Dev($"고양이 [{name}]: 기억 칸 킁킁 {_memoryCell}");
                return;
            }
            if (Time.time < _sniffUntil2) return;
            if (_heat.TryGetValue(_memoryCell, out float h)) _heat[_memoryCell] = h * 0.5f; // 확인했으니 반쯤 잊는다
            CancelMemoryVisit();
            GoToNextSpot();
        }

        private void CancelMemoryVisit()
        {
            _memoryVisit = false;
            _sniffUntil2 = -1f;
            if (Sniffing.Value) Sniffing.Value = false;
        }
    }
}
