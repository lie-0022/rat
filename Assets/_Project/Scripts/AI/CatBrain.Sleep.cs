using RatGame.Core;
using RatGame.Data;
using RatGame.Player;
using Unity.Netcode;
using UnityEngine;

namespace RatGame.AI
{
    /// <summary>고양이 잠 — 얕은/깊은 잠 파동과 깸 (design/cat-ideas/08). CatBrain.cs에서 옮김 (고양이 203).</summary>
    public partial class CatBrain
    {
        // 얕은 잠 ↔ 깊은 잠 파동. 얕은 잠에서 자극 → 한쪽 눈 뜸(HalfAwake) → 더 자극 있으면 깸, 없으면 다시 잔다.
        // 깊은 잠은 임계 ×2(접시 깨짐 수준)만 HalfAwake로. 전환 3s 전 예고 단계(꼬리 씰룩)가 있어 쥐가 읽을 수 있다.
        private void TickSleep()
        {
            var phase = SleepPhase.Value;
            bool deep = phase == CatSleepPhase.Deep || phase == CatSleepPhase.ToLight;
            float threshold = _balance.CatSuspicionThreshold * (deep ? _balance.CatDeepWakeGaugeMul : 1f);
            // 게이지가 임계를 넘거나, 접시 깨짐·함정·찍찍(즉시 조사 소음)이면 깬다. 그 밖의 작은 자극은 자는 동안 잊는다 — 남겨 두면 깬 뒤 엉뚱한 곳을 조사하러 간다
            bool stimulated = _senses.HasNewStimulus && (_senses.SuspicionGauge.Value >= threshold || _senses.ImmediateInvestigate);
            if (_senses.HasNewStimulus && !stimulated) _senses.ConsumeStimulus();

            if (phase == CatSleepPhase.HalfAwake)
            {
                // 눈을 뜬 사이 보거나 새 자극 → 깸
                if (_senses.VisibleTarget != null || stimulated)
                {
                    // 게으름뱅이는 바로 못 일어난다 — 기지개 1.5s(텔레그래프) 뒤 조사 (design/cat-ideas/01)
                    float stretch = Personality != null ? Personality.WakeStretchSeconds : 0f;
                    if (stretch > 0f) { _investigatePos = _senses.LastStimulusPos; _senses.ConsumeStimulus(); Log.Dev($"고양이 [{name}]: 으쌰… 기지개 ({stretch}s)"); EnterBlunder(CatBlunderKind.Stretch, stretch); return; }
                    EnterSuspicious(); return;
                }
                if (Time.time >= _phaseUntil) EnterSleepPhase(_phaseAfterHalfAwake);
                return;
            }
            if (stimulated)
            {
                _senses.ConsumeStimulus();
                _phaseAfterHalfAwake = phase == CatSleepPhase.Deep || phase == CatSleepPhase.ToLight ? CatSleepPhase.Light : phase;
                EnterSleepPhase(CatSleepPhase.HalfAwake);
                return;
            }
            if (Time.time >= _sleepUntil && phase == CatSleepPhase.Light)
            {
                // 다 잤다 (깊은 잠 중엔 안 깬다 — 얕은 잠으로 돌아온 뒤). 아직 잠자리 위라 Arrived가 참이므로 다음 스팟을 바로 고른다
                SetState(CatState.Patrol);
                GoToNextSpot();
                return;
            }
            if (Time.time < _phaseUntil) return;
            switch (phase)
            {
                case CatSleepPhase.Light: EnterSleepPhase(CatSleepPhase.ToDeep); break;
                case CatSleepPhase.ToDeep: EnterSleepPhase(CatSleepPhase.Deep); break;
                case CatSleepPhase.Deep: EnterSleepPhase(CatSleepPhase.ToLight); break;
                case CatSleepPhase.ToLight: EnterSleepPhase(CatSleepPhase.Light); break;
            }
        }

        private void EnterSleepPhase(CatSleepPhase phase)
        {
            SleepPhase.Value = phase;
            float sense;
            float duration;
            switch (phase)
            {
                case CatSleepPhase.Light: sense = _balance.CatSleepLightSense; duration = Random.Range(_balance.CatSleepLightRange.x, _balance.CatSleepLightRange.y); break;
                case CatSleepPhase.ToDeep: sense = _balance.CatSleepLightSense; duration = _balance.CatSleepForecastSeconds; break;
                case CatSleepPhase.Deep: sense = _balance.CatSleepDeepSense; duration = Random.Range(_balance.CatSleepDeepRange.x, _balance.CatSleepDeepRange.y); break;
                case CatSleepPhase.ToLight: sense = _balance.CatSleepDeepSense; duration = _balance.CatSleepForecastSeconds; break;
                default: sense = _balance.CatHalfAwakeSense; duration = _balance.CatHalfAwakeSeconds; break;
            }
            _senses.SensitivityMultiplier = sense;
            _phaseUntil = Time.time + duration;
            Log.Dev($"고양이 [{name}]: 잠 {phase} ({duration:0.0}s, 감각 {sense})");
            // 깊은 잠 신호를 소리(자막)로도 — 꼬리 씰룩은 눈으로만 보여서 벽 너머에선 모른다 (고양이 152)
            if (phase == CatSleepPhase.Deep) CatCueClientRpc((byte)CatCueKind.Snore, transform.position);
            else if (phase == CatSleepPhase.ToLight) CatCueClientRpc((byte)CatCueKind.SnoreStop, transform.position);
        }
    }
}
