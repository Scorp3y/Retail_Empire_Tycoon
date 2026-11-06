using System;
using RetailEmpireTycoon.UI.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RetailEmpireTycoon.StoreOperations
{
    internal sealed class ShopStatisticsPanel
    {
        private readonly ShopUi ui;
        private readonly StoreOperations shop;
        private readonly RectTransform panel, overview, rating;
        private readonly TMP_Text[] metrics, factors;
        private readonly TMP_Text score;
        private readonly Button overviewTab, ratingTab;
        private float nextRefresh;

        internal ShopStatisticsPanel(ShopUi ui, Transform canvas, StoreOperations shop, bool showRating, Action close)
        {
            this.ui = ui; this.shop = shop;
            panel = ui.Modal("Store statistics", canvas, new Vector2(598, 543));
            var header = ui.Rect("Statistics header", panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -10), new Vector2(578, 54));
            ui.Panel(header, ShopUiTheme.Ink);
            ui.Icon(header, ShopIcon.Statistics, new Vector2(-254, 0), new Vector2(24, 24), ShopUiTheme.Paper);
            ui.Heading(header, "Статистика магазина", "Store statistics", new Vector2(56, -10), new Vector2(448, 36), 24).color = ShopUiTheme.Paper;
            ui.IconButton(header, ShopIcon.Close, new Vector2(529, -9), 34, close);
            overviewTab = ui.Button(panel, "Обзор", "Overview", new Vector2(24, -79), new Vector2(266, 38), () => Select(false));
            ratingTab = ui.Button(panel, "Рейтинг", "Rating breakdown", new Vector2(308, -79), new Vector2(266, 38), () => Select(true));
            overview = Page("Sales overview");
            rating = Page("Rating factors");
            metrics = new TMP_Text[6];
            var names = new[] {"Sales revenue", "Wages paid", "Checkout sales", "Units sold", "Average receipt", "Store rating"};
            for (int i = 0; i < metrics.Length; i++)
            {
                var tile = ui.Rect(names[i], overview, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2((i % 2) * 284, -(i / 2) * 90), new Vector2(266, 78));
                ui.Panel(tile, ShopUiTheme.Line);
                ui.Label(tile, names[i], new Vector2(16, -9), new Vector2(232, 22), 15);
                metrics[i] = ui.Label(tile, "", new Vector2(16, -34), new Vector2(232, 34), 23);
                metrics[i].fontStyle = FontStyles.Bold;
            }
            ui.Label(overview, "Revenue, units and payroll are tracked from this update. Checkout sales includes earlier receipts.",
                new Vector2(0, -285), new Vector2(548, 58), 15).color = ShopUiTheme.Muted;

            score = ui.Label(rating, "", Vector2.zero, new Vector2(550, 44), 30);
            score.fontStyle = FontStyles.Bold;
            var labels = new[] {"Customer reputation", "Shelves with stock", "Product variety", "Store size",
                "Decorations", "Dirt in the store", "Checkout waits over 35 seconds"};
            factors = new TMP_Text[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                var row = ui.Rect(labels[i], rating, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -47 - i * 36), new Vector2(550, 32));
                if (i % 2 == 0) ui.Panel(row, ShopUiTheme.Line);
                ui.Label(row, labels[i], new Vector2(12, -5), new Vector2(400, 24), 16);
                factors[i] = ui.Label(row, "", new Vector2(423, -5), new Vector2(115, 24), 16);
                factors[i].alignment = TextAlignmentOptions.Right;
            }
            ui.Label(rating, "The total stays between 1 and 5 stars. Sales and caught thieves help reputation. Walkouts and theft lower it.",
                new Vector2(0, -312), new Vector2(550, 48), 14).color = ShopUiTheme.Muted;
            Select(showRating);
        }

        private RectTransform Page(string name) => ui.Rect(name, panel, new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(24, -141), new Vector2(550, 376));

        private void Select(bool showRating)
        {
            overview.gameObject.SetActive(!showRating);
            rating.gameObject.SetActive(showRating);
            overviewTab.image.color = showRating ? ShopUiTheme.Line : ShopUiTheme.Green;
            ratingTab.image.color = showRating ? ShopUiTheme.Green : ShopUiTheme.Line;
            Refresh();
        }

        internal void Tick()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .5f;
            Refresh();
        }

        private void Refresh()
        {
            metrics[0].text = $"${shop.SalesRevenue:N0}";
            metrics[1].text = $"${shop.WagesPaid:N0}";
            metrics[2].text = $"{shop.CompletedSales:N0}";
            metrics[3].text = $"{shop.UnitsSold:N0}";
            metrics[4].text = $"${shop.AverageReceipt:N2}";
            metrics[5].text = score.text = $"{shop.Rating:0.00} / 5";
            var breakdown = shop.RatingFactors;
            var values = new[] {breakdown.Reputation, breakdown.Stock, breakdown.Variety, breakdown.Size,
                breakdown.Decoration, breakdown.Dirt, breakdown.Queue};
            for (int i = 0; i < values.Length; i++)
            {
                factors[i].text = values[i].ToString("+0.00;-0.00;0.00");
                factors[i].color = values[i] < 0 ? new Color(.57f, .23f, .16f) : ShopUiTheme.Ink;
            }
        }

        internal void Close() => ui.CloseModal(panel);
    }
}
