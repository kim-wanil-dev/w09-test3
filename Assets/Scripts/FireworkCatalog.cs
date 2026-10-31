using System.Collections.Generic;

namespace Fireworks
{
    public static class FireworkCatalog
    {
        private static readonly CardDefinition[] _cards =
        {
            new CardDefinition(1, "빨간 국화탄", FireworkColor.Red, 1, 3, 2, 0),
            new CardDefinition(2, "초록 연쇄탄", FireworkColor.Green, 1, 1, 1, 3),
            new CardDefinition(3, "파란 고리탄", FireworkColor.Blue, 1, 3, 0, 0),
            new CardDefinition(4, "빨간 대형 유성탄", FireworkColor.Red, 2, 4, 3, 0),
            new CardDefinition(5, "파란 송풍탄", FireworkColor.Blue, 1, 2, 0, 0),
            new CardDefinition(6, "파란 먹구름탄", FireworkColor.Blue, 2, 3, 0, 0),
            new CardDefinition(7, "무지개탄", FireworkColor.Multicolor, 2, 2, 1, 1),
            new CardDefinition(8, "파란 메아리탄", FireworkColor.Blue, 1, 2, 0, 0),
            new CardDefinition(9, "초록 꽃다발탄", FireworkColor.Green, 2, 2, 1, 0),
            new CardDefinition(10, "파란 별탄", FireworkColor.Blue, 2, 4, 0, 0),
            new CardDefinition(11, "빨간 개막탄", FireworkColor.Red, 1, 3, 2, 0),
            new CardDefinition(12, "초록 피날레탄", FireworkColor.Green, 1, 1, 1, 0),
            new CardDefinition(13, "초록 잔광탄", FireworkColor.Green, 1, 1, 1, 2)
        };

        public static IReadOnlyList<CardDefinition> Cards => _cards;

        /// <summary>카드 ID에 해당하는 정의를 반환한다.</summary>
        public static CardDefinition Get(int id) { return _cards[id - 1]; }
    }
}
