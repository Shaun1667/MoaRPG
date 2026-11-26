using Moa.Core.Gacha;
using Moa.Core.Growth;
using Moa.Unity;
using Moa.Unity.Data;
using UnityEditor;
using UnityEngine;

namespace Moa.Editor.Debugging
{
    /// <summary>가챠 (3-7): 소원석 · 배너 종류마다 천장 카운트 · 픽업 보장 · 합계 · 보유한 것, [상태 변경] 소원석 +N · 카운트 지정 · 초기화 · 1회 / 10회 · 화면 열기.</summary>
    public sealed class GachaSection : IDebugSection
    {
        public string Title => "가챠 (소원 · 천장)";

        int _addAmount = 10;
        int _kind;
        int _five;
        int _four;
        bool _guaranteed;
        int _banner;

        public void Draw(GameRoot root)
        {
            if (root != null && root.DebugGacha == null)
            {
                EditorGUILayout.HelpBox("이 씬에는 가챠가 연결되지 않았습니다 (GameRoot의 Gacha 칸 · 성장 필요, 메뉴 MoaRPG > Setup > Test Field).", MessageType.None);
                return;
            }

            using (new EditorGUI.DisabledScope(root == null))
            {
                GachaAccount g = root != null ? root.DebugGacha : null;
                EditorGUILayout.LabelField("소원석", g != null ? g.Wallet.Get(Currency.Wish) + " · 이번 판 뽑기 " + g.PullCount + " · 마지막 막힘 " + g.LastBlock + " · 난수 " + g.Random.State : "-");
                foreach (BannerKind k in System.Enum.GetValues(typeof(BannerKind)))
                {
                    GachaPity p = g != null ? g.Pity(k) : null;
                    EditorGUILayout.LabelField("천장 " + k, p != null
                        ? "★5 뒤 " + p.SinceFive + "회 · ★4 뒤 " + p.SinceFour + "회 · 픽업 보장 " + (p.FeaturedGuaranteed ? "있음" : "없음") + " · 합계 " + p.Pulls + "회 (★5 " + p.FiveCount + " · ★4 " + p.FourCount + ")"
                        : "-");
                }
                var banners = root != null ? root.DebugGachaBanners : null;
                if (banners != null)
                    foreach (var b in banners)
                    {
                        double[] c = b.ConsolidatedRates();
                        EditorGUILayout.LabelField("배너 " + b.Id, b.Kind + " · 비용 " + b.Cost + " · 종합 ★5 " + (c[2] * 100).ToString("0.00") + "% · ★4 " + (c[1] * 100).ToString("0.00") + "%" + (b.HasFeatured ? " · 픽업 " + b.Featured[0].Id : ""));
                    }
                if (g != null)
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (var id in g.Owned)
                    {
                        if (sb.Length > 0) sb.Append(", ");
                        sb.Append(id).Append(g.KindOf(id) == GachaItemKind.Character ? " 각성 " + g.Awakening(id) : " 재련 " + g.Refinement(id));
                    }
                    EditorGUILayout.LabelField("보유", sb.ToString(), EditorStyles.wordWrappedLabel);
                }

                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    _addAmount = EditorGUILayout.IntField("소원석", _addAmount);
                    if (GUILayout.Button("[상태 변경] +N", GUILayout.Width(110))) root.DebugGachaAddWishes(_addAmount);
                    if (GUILayout.Button("[상태 변경] −N", GUILayout.Width(110))) root.DebugGachaAddWishes(-_addAmount);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    _kind = EditorGUILayout.Popup(_kind, System.Enum.GetNames(typeof(BannerKind)), GUILayout.Width(140));
                    _five = EditorGUILayout.IntField("★5 뒤", _five);
                    _four = EditorGUILayout.IntField("★4 뒤", _four);
                    _guaranteed = EditorGUILayout.ToggleLeft("보장", _guaranteed, GUILayout.Width(60));
                    if (GUILayout.Button("[상태 변경] 카운트 지정", GUILayout.Width(150))) root.DebugGachaSetPity((BannerKind)_kind, _five, _four, _guaranteed);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    int n = banners != null ? banners.Length : 0;
                    string[] names = new string[n];
                    for (int i = 0; i < n; i++) names[i] = banners[i].Id;
                    if (n > 0) _banner = EditorGUILayout.Popup(Mathf.Clamp(_banner, 0, n - 1), names, GUILayout.Width(180));
                    int multi = g != null ? g.Rules.MultiPull : 10;
                    if (GUILayout.Button("[상태 변경] 1회")) root.DebugGachaPull(_banner, 1);
                    if (GUILayout.Button("[상태 변경] " + multi + "회")) root.DebugGachaPull(_banner, multi);
                    if (GUILayout.Button("[상태 변경] 초기화")) root.DebugGachaReset();
                }
                if (GUILayout.Button(root != null && root.DebugGachaOpen ? "[보기] 가챠 화면 닫기" : "[보기] 가챠 화면 열기")) root.DebugSetGachaOpen(!root.DebugGachaOpen);
                EditorGUILayout.HelpBox("G로 가챠 화면 — 배너 탭 · 1회 / 10회 · [확률 정보]. 천장은 배너 종류마다 따로, 저장된다. 초기화는 천장 · 보유를 처음으로(소원석은 그대로). 실제 결제 없음 — 소원석은 상자 · 처치 · 여기서만.", MessageType.None);
            }
        }
    }
}
