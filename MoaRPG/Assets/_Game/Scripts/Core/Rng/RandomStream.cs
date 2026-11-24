namespace Moa.Core.Rng
{
    /// <summary>
    /// 난수 용도. 용도마다 따로 굴러서, 한 용도를 더 뽑아도 다른 용도의 결과는 그대로다 (핵심 설계 규칙 4).
    /// 값은 에셋 · 저장에 정수로 남을 수 있으니 끝에만 추가한다.
    /// </summary>
    public enum RandomStream
    {
        Gacha = 0,
        Drop = 1,
        Combat = 2,
        Presentation = 3,
    }
}
