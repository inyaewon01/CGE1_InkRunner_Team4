using UnityEditor;

namespace InkRunner.BlackInkPrototype.Editor
{
    /// <summary>Keep the original test command/menu compatible with the updated endless scene.</summary>
    public static class BlackInkValidation
    {
        public static void BuildAndValidateBatch() => EndlessInkValidation.BuildAndValidateBatch();

        [MenuItem("Tools/InkRunner/Validate Black Ink Prototype (Play Mode)")]
        public static void StartValidation() => EndlessInkValidation.StartValidation();
    }
}
