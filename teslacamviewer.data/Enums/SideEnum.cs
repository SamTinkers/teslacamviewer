namespace teslacamviewer.data.Enums
{
    public enum SideEnum
    {
        Front = 0,
        Back = 1,
        // Legacy values kept for any pre-existing rows; modern Tesla cameras use the
        // *_repeater / *_pillar variants below.
        Left = 2,
        Right = 3,
        // HW4 (post-2021) Tesla camera layout — 6 angles total.
        LeftRepeater = 4,
        RightRepeater = 5,
        LeftPillar = 6,
        RightPillar = 7
    }
}
