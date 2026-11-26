using System;

namespace Moa.Core.Growth
{
    /// <summary>
    /// 저장 스냅샷 하나 (핵심 설계 규칙 3, 3-5에서 처음): 형식 번호 · 재화 · 캐릭터 성장. 가챠 기록 · 월드 진행은 그 항목에서 칸을 더하고 형식 번호를 올린다.
    /// 글이 아니라 키와 값만 (재화는 Currency 이름, 캐릭터는 CharacterData.saveId). JSON 읽기 · 쓰기는 Unity 쪽 SaveStore(JsonUtility) — 그래서 공개 필드.
    /// </summary>
    [Serializable]
    public sealed class SaveSnapshot
    {
        public int version;
        public CurrencySave[] wallet = new CurrencySave[0];
        public MemberSave[] characters = new MemberSave[0];
        /// <summary>3-6 (형식 2부터): 월드 진행 — 순간이동 지점 · 상자 · 퍼즐. 형식 1 파일은 <see cref="SaveFormat.Upgrade"/>가 빈 것으로 채운다.</summary>
        public WorldSave world = new WorldSave();
    }

    /// <summary>월드 진행 (3-6): 활성화한 순간이동 지점 · 연 상자 · 푼 퍼즐의 id (Core WorldProgress). 압력판 하나하나는 저장하지 않는다.</summary>
    [Serializable]
    public sealed class WorldSave
    {
        public string[] waypoints = new string[0];
        public string[] chests = new string[0];
        public string[] puzzles = new string[0];
    }

    [Serializable]
    public sealed class CurrencySave
    {
        public string id;
        public int amount;
    }

    /// <summary>캐릭터 한 명의 성장. 무기 레벨은 1차에서는 그 캐릭터가 든 무기 칸의 것 (무기 보관함이 생기면 무기 쪽으로 옮긴다).</summary>
    [Serializable]
    public sealed class MemberSave
    {
        public string id;
        public int level;
        public int exp;
        public int ascension;
        public int skillLevel;
        public int burstLevel;
        public int weaponLevel;
        public int weaponExp;
        /// <summary>무기 재련 (아직 없음 — 늘 1, 자리만).</summary>
        public int refinement;

        public MemberSave Clone() => (MemberSave)MemberwiseClone();
    }

    /// <summary>저장 형식 (3-5). 새 상태 = 스냅샷 필드 + <see cref="Version"/> 올림 + 옛 형식 옮기기(<see cref="Upgrade"/>) + 왕복 테스트 (MY_DEV_GUIDE).</summary>
    public static class SaveFormat
    {
        /// <summary>지금 형식 번호. 1 = 3-5 (재화 · 캐릭터), 2 = 3-6 (+ world).</summary>
        public const int Version = 2;

        /// <summary>
        /// 옛 형식을 지금 형식으로 옮긴다 (읽은 직후, Validate 전에). 옮겼으면 원래 형식 번호, 옮길 것이 없으면(지금 형식 · 모르는 번호 · null) 0.
        /// 1 → 2: world가 없으면 빈 것 (지점 · 상자 · 퍼즐 없음).
        /// </summary>
        public static int Upgrade(SaveSnapshot s)
        {
            if (s == null || s.version < 1 || s.version >= Version) return 0;
            int from = s.version;
            if (s.version == 1)
            {
                if (s.world == null) s.world = new WorldSave();
                s.version = 2;
            }
            return from;
        }

        /// <summary>읽은 스냅샷을 쓸 수 있는지 — 형식 번호가 1 ~ 지금이고 배열이 있어야. 값의 범위는 Progression.Load가 고친다.</summary>
        public static bool Validate(SaveSnapshot s, out string reason)
        {
            if (s == null) { reason = "empty"; return false; }
            if (s.version < 1) { reason = "no version"; return false; }
            if (s.version > Version) { reason = "version " + s.version + " is newer than " + Version; return false; }
            if (s.wallet == null || s.characters == null) { reason = "missing arrays"; return false; }
            if (s.version >= 2 && (s.world == null || s.world.waypoints == null || s.world.chests == null || s.world.puzzles == null)) { reason = "missing world"; return false; }
            foreach (var m in s.characters)
                if (m == null || string.IsNullOrEmpty(m.id)) { reason = "character without id"; return false; }
            reason = null;
            return true;
        }
    }
}
