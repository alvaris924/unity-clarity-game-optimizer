namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// What a graph node stands for, in Unity terms. Exporters map these onto their own vocabulary; the
    /// archify legend pairs each kind with exactly one of its component types.
    /// </summary>
    internal enum NodeKind
    {
        /// <summary>Gameplay and systems code that ships in the player.</summary>
        Runtime = 0,

        /// <summary>Editor-only tools and windows.</summary>
        Editor = 1,

        /// <summary>Data and content: ScriptableObject databases, Resources folders, catalogs.</summary>
        Data = 2,

        /// <summary>Wrappers and SDKs for backend services: cloud saves, analytics, ads.</summary>
        Service = 3,

        /// <summary>Event and messaging code that connects the rest.</summary>
        Messaging = 4,

        /// <summary>Tests and validation.</summary>
        Test = 5,

        /// <summary>Third-party packages and plugins.</summary>
        External = 6,
    }
}
