using System.ComponentModel.DataAnnotations;

namespace SignUpDemo;

public class SignUp
{
    [Required, EmailAddress, UniqueEmail]
    public string Email { get; set; } = "";
}

// Two async checks on one model, both using the request's AppDb.
public class SignUpWithUserName
{
    [Required, EmailAddress, UniqueEmail]
    public string Email { get; set; } = "";

    [Required, UniqueUserName]
    public string UserName { get; set; } = "";
}
