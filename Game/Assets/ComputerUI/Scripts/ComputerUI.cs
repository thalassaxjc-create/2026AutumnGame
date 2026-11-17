using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ComputerUI : MonoBehaviour
{
    public enum ProductionStep { Materials, Bamboo, Weaving, Lacquering, Drying, Delivery }

    [SerializeField] Image[] tabs;
    [SerializeField] Color inactiveTab;
    [SerializeField] Color activeTab;
    [SerializeField] GameObject pagePanel;
    [SerializeField] GameObject orderPage;
    [SerializeField] GameObject orderDetails;
    [SerializeField] Image portrait;
    [SerializeField] RectTransform progressTrack;
    [SerializeField] RectTransform progressFill;
    [SerializeField] TMP_Text progressLabel;
    [SerializeField] GameObject[] completedMarkers;
    [SerializeField] TMP_Text fundsLabel;
    [SerializeField] TMP_Text timeLabel;
    [SerializeField] TMP_Text dayLabel;
    ComputerOrderView orderView;

    public event Action CloseRequested;

    void OnEnable()
    {
        SelectPage(-1);
        if (WorkshopOrder.Current != null)
        {
            if (orderView == null)
            {
                orderView = gameObject.AddComponent<ComputerOrderView>();
                orderView.Configure(this, tabs[0].rectTransform, (RectTransform)orderPage.transform,
                    (RectTransform)pagePanel.transform.parent, fundsLabel.font);
            }
            orderView.Refresh(WorkshopOrder.Current);
        }
    }

    public void ShowOrders()
    {
        SelectPage(0);
        WorkshopOrder.Current?.ReadOrder();
    }
    public void ShowShop() => SelectPage(1);
    public void ShowManual() => SelectPage(2);
    public void RequestClose() => CloseRequested?.Invoke();

    void SelectPage(int page)
    {
        for (int i = 0; i < tabs.Length; i++)
            tabs[i].color = i == page ? activeTab : inactiveTab;

        orderDetails.SetActive(false);
        orderPage.SetActive(page == 0);
        pagePanel.SetActive(page >= 0);
    }

    public void ToggleOrderDetails()
    {
        if (orderPage.activeInHierarchy)
            orderDetails.SetActive(!orderDetails.activeSelf);
    }

    public void SetFunds(int funds) => fundsLabel.text = funds.ToString();
    public void SetTime(int hour, int minute) => timeLabel.text = $"{hour:00}:{minute:00}";
    public void SetDay(int day) => dayLabel.text = $"天数 {day:00}";

    public void SetProgress(float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        progressFill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, progressTrack.rect.width * fraction);
        progressFill.gameObject.SetActive(fraction > 0f);
        progressLabel.text = $"订单进度   {Mathf.RoundToInt(fraction * 100f)}%";
    }

    public void SetStepCompleted(ProductionStep step, bool completed)
    {
        completedMarkers[(int)step].SetActive(completed);
    }

    public void SetPortrait(Sprite sprite)
    {
        portrait.sprite = sprite;
        portrait.enabled = sprite != null;
    }
}
