using System.Collections;
using UnityEngine;


public class TransithionSimpleFade : MonoBehaviour
{
    [SerializeField] private CanvasGroup blackFadeGroup;  // 黒Image + CanvasGroup (alpha=0スタート)
    public void PlayNode(VideoNode node)
    {
        // 黒フェードアウト
        if (blackFadeGroup != null) StartCoroutine(Fade(blackFadeGroup, 1f, 0.4f));  // 0.4秒で黒へ

        //player.ClearTarget();  // 古いフレーム残り防止
        //player.PlayClip(node.videoClip);

        // 少し待ってフェードイン（最初のフレームが出るまで）
        StartCoroutine(FadeInAfter(0.1f, 0.5f));
    }

    private IEnumerator Fade(CanvasGroup group, float target, float duration)
    {
        float start = group.alpha;
        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime / duration;
            group.alpha = Mathf.Lerp(start, target, t);
            yield return null;
        }
        group.alpha = target;
    }

    private IEnumerator FadeInAfter(float delay, float duration)
    {
        yield return new WaitForSeconds(delay);
        if (blackFadeGroup != null)
            StartCoroutine(Fade(blackFadeGroup, 0f, duration));
    }
}
