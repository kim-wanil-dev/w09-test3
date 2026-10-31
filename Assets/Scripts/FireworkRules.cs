using System;
using System.Collections.Generic;

namespace Fireworks
{
    public enum LaunchMode { Direct, Chain }
    public enum FireworkColor { Red, Green, Blue, Multicolor }

    public sealed class CardDefinition
    {
        public int Id { get; }
        public string Name { get; }
        public FireworkColor Color { get; }
        public int Cost { get; }
        public int BaseScore { get; }
        public int Smoke { get; }
        public int ChainBonus { get; }

        /// <summary>카드의 식별자, 표시 정보와 규칙 수치를 설정한다.</summary>
        public CardDefinition(int id, string name, FireworkColor color, int cost, int baseScore, int smoke, int chainBonus)
        {
            Id = id; Name = name; Color = color; Cost = cost; BaseScore = baseScore; Smoke = smoke; ChainBonus = chainBonus;
        }
    }

    public sealed class PlannedCard
    {
        public CardDefinition Card { get; }
        public LaunchMode Mode { get; }
        public int Order { get; }

        /// <summary>카드와 발사 방식, 직접 발사 순서를 가진 계획을 만든다.</summary>
        public PlannedCard(CardDefinition card, LaunchMode mode, int order)
        {
            Card = card; Mode = mode; Order = order;
        }
    }

    public sealed class ExplosionRecord
    {
        public int Slot { get; internal set; }
        public int CauseSlot { get; internal set; }
        public CardDefinition Card { get; internal set; }
        public LaunchMode Mode { get; internal set; }
        public int Score { get; internal set; }
        public int SmokeBefore { get; internal set; }
        public int SmokeAfter { get; internal set; }
    }

    public sealed class TurnResult
    {
        private readonly List<ExplosionRecord> _explosions = new List<ExplosionRecord>();
        private readonly List<int> _misfires = new List<int>();
        public IReadOnlyList<ExplosionRecord> Explosions => _explosions;
        public IReadOnlyList<int> Misfires => _misfires;
        public int AudienceBonus { get; internal set; }
        public int TotalScore { get; internal set; }
        public int EndSmoke { get; internal set; }

        /// <summary>폭발 기록을 결과에 추가한다.</summary>
        internal void AddExplosion(ExplosionRecord record) { _explosions.Add(record); }
        /// <summary>불발된 슬롯을 결과에 추가한다.</summary>
        internal void AddMisfire(int slot) { _misfires.Add(slot); }
    }

    public static class FireworkRules
    {
        /// <summary>장전 계획과 연기, 바람, 관객 조건으로 턴 결과를 계산한다.</summary>
        public static TurnResult Calculate(PlannedCard[] slots, int smoke, int wind, bool threeColors, int turn)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            TurnResult result = new TurnResult();
            bool[] exploded = new bool[slots.Length];
            smoke = Math.Max(0, Math.Min(5, smoke));
            List<int> direct = new List<int>();
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null && slots[i].Mode == LaunchMode.Direct) direct.Add(i);
            direct.Sort((a, b) => slots[a].Order.CompareTo(slots[b].Order));
            foreach (int i in direct) Explode(i, -1, slots, exploded, result, ref smoke, turn);
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null && slots[i].Mode == LaunchMode.Chain && !exploded[i]) result.AddMisfire(i);
            bool red = false, green = false, blue = false, redGreen = false;
            for (int i = 0; i < result.Explosions.Count; i++)
            {
                FireworkColor color = result.Explosions[i].Card.Color;
                red |= color == FireworkColor.Red; green |= color == FireworkColor.Green; blue |= color == FireworkColor.Blue;
                if (i > 0 && result.Explosions[i - 1].Card.Color == FireworkColor.Red && color == FireworkColor.Green) redGreen = true;
            }
            result.AudienceBonus = threeColors ? (red && green && blue ? 4 : 0) : (redGreen ? 3 : 0);
            result.TotalScore += result.AudienceBonus;
            result.EndSmoke = Math.Max(0, smoke - wind);
            return result;
        }

        /// <summary>폭발 점수와 연기를 기록하고 인접 연쇄를 왼쪽부터 처리한다.</summary>
        private static void Explode(int index, int cause, PlannedCard[] slots, bool[] exploded, TurnResult result, ref int smoke, int turn)
        {
            if (exploded[index]) return;
            PlannedCard slot = slots[index];
            CardDefinition card = slot.Card;
            exploded[index] = true;
            int before = smoke;
            int baseScore = card.BaseScore;
            if (card.Id == 9 && result.Explosions.Count >= 2) baseScore += 4;
            int score = Math.Max(0, baseScore - smoke / 2) + (slot.Mode == LaunchMode.Chain ? card.ChainBonus : 0);
            if ((card.Id == 11 && turn == 0) || (card.Id == 12 && turn == 2)) score += 3;
            bool hasRed = false, hasGreen = false, hasBlue = false;
            foreach (ExplosionRecord previous in result.Explosions)
            {
                hasRed |= previous.Card.Color == FireworkColor.Red;
                hasGreen |= previous.Card.Color == FireworkColor.Green;
                hasBlue |= previous.Card.Color == FireworkColor.Blue;
            }
            if ((card.Id == 7 && hasRed && hasGreen && hasBlue) || (card.Id == 13 && hasRed)) score *= 2;
            smoke = Math.Min(5, smoke + card.Smoke);
            if (card.Color == FireworkColor.Blue)
            {
                int removed = Math.Min(smoke, card.Id == 5 || card.Id == 6 ? 2 : 1);
                smoke -= removed;
                if (card.Id == 6 && removed == 2) score += 4;
            }
            result.TotalScore += score;
            result.AddExplosion(new ExplosionRecord { Slot = index, CauseSlot = cause, Card = card, Mode = slot.Mode, Score = score, SmokeBefore = before, SmokeAfter = smoke });
            for (int next = Math.Max(0, index - 1); next <= Math.Min(slots.Length - 1, index + 1); next++)
            {
                if (next == index || exploded[next] || slots[next] == null || slots[next].Mode != LaunchMode.Chain) continue;
                CardDefinition target = slots[next].Card;
                bool matches = target.Id == 7 || ((target.Id == 2 || target.Id == 13) && (card.Color == FireworkColor.Red || card.Id == 7));
                if (matches) Explode(next, index, slots, exploded, result, ref smoke, turn);
            }
        }
    }
}
