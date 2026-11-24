using Moa.Unity;

namespace Moa.Editor.Debugging
{
    /// <summary>
    /// 디버그 창 섹션 하나. 시스템이 생기면 섹션 하나를 더하고 <see cref="DebugWindow"/>의 목록에 한 줄 넣는다.
    /// 게임 상태를 바꾸는 버튼에는 [상태 변경], 보기만 바꾸면 [보기]를 붙인다 (MY_DEV_GUIDE 2번).
    /// </summary>
    public interface IDebugSection
    {
        string Title { get; }

        /// <summary>root는 플레이 중일 때만 있다 (없으면 null).</summary>
        void Draw(GameRoot root);
    }
}
