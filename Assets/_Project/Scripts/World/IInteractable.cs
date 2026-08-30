namespace RatGame.World
{
    /// <summary>E 상호작용 대상 (docs/04). ServerInteract는 호스트에서만 호출된다.</summary>
    public interface IInteractable
    {
        string PromptText { get; }   // "구출하기", "문 열기"
        float HoldSeconds { get; }   // 0 = 즉시
        bool CanInteract(ulong clientId);
        void ServerInteract(ulong clientId);
    }
}
