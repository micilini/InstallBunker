namespace InstallBunker.Common.PackageContainer;

public enum PackageContainerErrorKind
{
    Unknown = 0,

    PackageNotFound,
    InvalidMagic,
    InvalidHeaderLength,
    HeaderParseFailed,
    UnsupportedVersion,
    InvalidCiphertextLength,

    AuthenticationFailed,
    IntegrityCheckFailed,

    InvalidEntryPath,
    InvalidEntryName,
    LimitsExceeded,
    ExtractionFailed,

    UnexpectedEndOfStream
}