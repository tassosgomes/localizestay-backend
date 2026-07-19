namespace LocalizeStay.Modules.Inventory.Domain.Partners;

public sealed record Contact
{
    public string Name { get; }
    public string Email { get; }
    public string Phone { get; }

    public Contact(string name, string email, string phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);

        if (name.Length is < 2 or > 120)
        {
            throw new ArgumentException("Name must be between 2 and 120 characters.", nameof(name));
        }

        if (email.Length > 255)
        {
            throw new ArgumentException("Email must be at most 255 characters.", nameof(email));
        }

        if (phone.Length is < 8 or > 30)
        {
            throw new ArgumentException("Phone must be between 8 and 30 characters.", nameof(phone));
        }

        Name = name.Trim();
        Email = email.Trim();
        Phone = phone.Trim();
    }
}
