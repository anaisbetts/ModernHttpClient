// The NSUrlError enum provided by Xamarin is missing some values.  
// To avoid a potential conflict, these are in a namespace specific to this library.

namespace ModernHttpClient.Foundation
{
    public enum NSUrlErrorExtended
    {
        Unknown = -1,
        Cancelled = -999,
        BadURL = -1000,
        TimedOut = -1001,
        UnsupportedURL = -1002,
        CannotFindHost = -1003,
        CannotConnectToHost = -1004,
        NetworkConnectionLost = -1005,
        DNSLookupFailed = -1006,
        HTTPTooManyRedirects = -1007,
        ResourceUnavailable = -1008,
        NotConnectedToInternet = -1009,
        RedirectToNonExistentLocation = -1010,
        BadServerResponse = -1011,
        UserCancelledAuthentication = -1012,
        UserAuthenticationRequired = -1013,
        ZeroByteResource = -1014,
        CannotDecodeRawData = -1015,
        CannotDecodeContentData = -1016,
        CannotParseResponse = -1017,

        // NEW
        InternationalRoamingOff = -1018,
        CallIsActive = -1019,
        DataNotAllowed = -1020,
        RequestBodyStreamExhausted = -1021,

        // SSL errors
        SecureConnectionFailed = -1200,
        ServerCertificateHasBadDate = -1201,
        ServerCertificateUntrusted = -1202,
        ServerCertificateHasUnknownRoot = -1203,
        ServerCertificateNotYetValid = -1204,
        ClientCertificateRejected = -1205,

        // NEW
        ClientCertificateRequired = -1206,

        CannotLoadFromNetwork = -2000,

        // Downoad and file I/O errors
        CannotCreateFile = -3000,
        CannotOpenFile = -3001,
        CannotCloseFile = -3002,
        CannotWriteToFile = -3003,
        CannotRemoveFile = -3004,
        CannotMoveFile = -3005,
        DownloadDecodingFailedMidStream = -3006,
        DownloadDecodingFailedToComplete = -3007,

        FileDoesNotExist = -1100,
        FileIsDirectory = -1101,
        NoPermissionsToReadFile = -1102,
        DataLengthExceedsMaximum = -1103,

        BackgroundSessionRequiresSharedContainer = -995,
        BackgroundSessionInUseByAnotherProcess = -996,
        BackgroundSessionWasDisconnected = -997
    }
}
