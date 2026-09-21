namespace ChangeLens.Core.Publication.Models;

/// <summary>Defines how a published track is rendered.</summary>
public enum ReadingShape
{
    /// <summary>Render ordered steps.</summary>
    Walk,

    /// <summary>Render participant relationships.</summary>
    ParticipantMap,

    /// <summary>Render purpose cards.</summary>
    PurposeCards,

    /// <summary>Render participants without a populated track field.</summary>
    ParticipantList,
}
