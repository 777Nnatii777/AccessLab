namespace AccessLab.Application.Authentication;

public sealed record FirstPasswordChangeRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword
);