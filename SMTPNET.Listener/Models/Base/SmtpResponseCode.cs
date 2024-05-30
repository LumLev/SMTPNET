namespace SMTPNET.Worker.Models.Base
{
    enum SmtpResponseCode
    {
        ServerConnectionError = 101,
        SystemStatus = 211,
        HelpMessage = 214,
        ServerReady = 220,
        ServerClosesTransmissionChannel = 221,
        AuthenticationSuccessful = 235,
        OK = 250,
        UserNotLocalWillForward = 251,
        CannotVerifyUser = 252,
        SecurityMechanismAccepted = 334,
        StartMailInput = 354,
        ServerNotAvailable = 421,
        MailboxExceededStorageLimit = 422,
        FileOverload = 431,
        NoResponseFromRecipientServer = 441,
        ConnectionDropped = 442,
        InternalLoop = 446,
        MailboxUnavailable = 450,
        LocalErrorInProcessing = 451,
        InsufficientSystemStorage = 452,
        TLSNotAvailable = 454,
        ParametersNotAccommodated = 455,
        LocalSpamFilterError = 471,
        SyntaxError = 500,
        SyntaxErrorInParametersOrArguments = 501,
        CommandNotImplemented = 502,
        BadSequenceOfCommands = 503,
        CommandParameterNotImplemented = 504,
        InvalidEmailAddress = 510,
        DnsError = 512,
        MailingExceedsRecipientServerLimits = 523,
        AuthenticationProblem = 530,
        AuthenticationFailed = 535,
        EncryptionRequired = 538,
        MessageRejectedBySpamFilter = 541,
        ServerUnavailable = 550,
        UserNotLocal = 551,
        MailboxFull = 552,
        SyntacticallyIncorrectMailAddress = 553,
        NoSmtpService = 554,
        ParametersNotRecognized = 555
    }




}

