using System.Text;
using Moa.Core.Ids;
using Moa.Core.Localization;
using Moa.Core.Rng;
using Moa.Unity.Data;
using Moa.Unity.UI;
using UnityEngine;

namespace Moa.Unity
{
    /// <summary>
    /// 컴포지션 루트 — 씬 연결은 여기 한 곳 (MY_DEV_GUIDE 9번). 싱글턴을 두지 않는다.
    /// 새 판 시작과 디버그 재시작은 같은 <see cref="StartRun"/>을 탄다.
    /// 개발자 진입점은 DebugXxx (에디터 디버그 창이 부른다).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameRoot : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [Tooltip("Phase 0 temporary label (text table check)")]
        [SerializeField] LocalizedLabel bootLabel;

        int _seed;
        RandomStreams _random;
        IdGenerator _ids;
        int _runCount;

        void Awake()
        {
            if (config == null) { Debug.LogError("GameRoot: GameConfig is not assigned"); enabled = false; return; }
            if (config.TextTable == null) { Debug.LogError("GameRoot: text table is not assigned in GameConfig"); enabled = false; return; }
            Text.Load(TextTable.ParseCsv(config.TextTable.text), config.DefaultLanguage);
            UiFont.ApplyTo(transform, config);
            StartRun(config.DefaultSeed);
        }

        void StartRun(int seed)
        {
            _seed = seed;
            _random = new RandomStreams(RandomStreams.SeedFromInt(seed));
            _ids = new IdGenerator();
            _runCount++;
            if (bootLabel != null) bootLabel.SetArgs(seed);
        }

        // ---- 디버그 진입점 (에디터 디버그 창 전용) ----

        public int DebugSeed => _seed;

        /// <summary>[상태 변경] 같은 시드로 새 판.</summary>
        public void DebugRestart() => StartRun(_seed);

        /// <summary>[상태 변경] 정한 시드로 새 판.</summary>
        public void DebugRestart(int seed) => StartRun(seed);

        /// <summary>[보기] 화면 언어 바꾸기.</summary>
        public void DebugSetLanguage(string language) => Text.SetLanguage(language);

        /// <summary>지금 상태 요약 (개발자용, 텍스트 표 대상 아님).</summary>
        public string DebugState()
        {
            var sb = new StringBuilder();
            sb.Append("run: ").Append(_runCount).AppendLine();
            sb.Append("seed: ").Append(_seed).Append(" (master ").Append(_random != null ? _random.MasterSeed.ToString() : "-").Append(')').AppendLine();
            if (_random != null)
            {
                foreach (RandomStream s in System.Enum.GetValues(typeof(RandomStream)))
                    sb.Append("  rng ").Append(s).Append(" state: ").Append(_random.Get(s).State).AppendLine();
            }
            sb.Append("ids issued: ").Append(_ids != null ? _ids.IssuedCount : 0).Append(", next: ").Append(_ids != null ? _ids.NextValue : 0).AppendLine();
            sb.Append("language: ").Append(Text.Language ?? "-");
            if (Text.Table != null) sb.Append(" / ").Append(string.Join(",", Text.Table.Languages)).Append(" (keys ").Append(Text.Table.Keys.Count).Append(')');
            return sb.ToString();
        }
    }
}
