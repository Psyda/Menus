namespace DoneYet.Services;

/// <summary>
/// General-purpose expense categories for a Canadian freelancer/sole prop,
/// loosely mapped to CRA T2125 lines. Each has plain-language examples because
/// nobody remembers which bucket "domain renewal" goes in at 11pm.
/// This is bookkeeping convenience, not tax advice.
/// </summary>
public static class TaxCategories
{
    public record Category(string Name, string Examples);

    public static readonly Category[] All =
    {
        new("Software & subscriptions",
            "Adobe, Figma, JetBrains, cloud hosting, SaaS tools, domain names, app store fees"),
        new("Office supplies & small equipment",
            "Pens, paper, printer ink, cables, a mouse — small stuff under ~$500"),
        new("Capital assets (big equipment)",
            "Computers, monitors, cameras, furniture, gear over ~$500 (depreciated via CCA)"),
        new("Advertising & promotion",
            "Online ads, business cards, portfolio site costs, sponsored posts"),
        new("Meals & entertainment (50%)",
            "Client lunch, coffee meeting — generally only 50% deductible"),
        new("Professional fees",
            "Accountant, lawyer, notary, business consultant"),
        new("Contract labour / subcontractors",
            "Paying another freelancer or contractor to help on client work"),
        new("Phone & internet",
            "Cell plan, home internet — claim the business-use percentage"),
        new("Rent (workspace)",
            "Studio/office rent, coworking membership"),
        new("Home office (business-use-of-home)",
            "Portion of rent/mortgage interest, utilities, property tax, home insurance"),
        new("Utilities",
            "Electricity, heat, water for a dedicated business space"),
        new("Insurance",
            "Liability insurance, equipment/business insurance"),
        new("Interest & bank charges",
            "Bank account fees, Stripe/PayPal/Wise fees, interest on business loans"),
        new("Business taxes, licences & memberships",
            "Business licence, professional dues, trade association memberships"),
        new("Travel",
            "Flights, hotels, trains, transit for business trips (not commuting)"),
        new("Motor vehicle",
            "Gas, maintenance, parking, insurance — business-use percentage, keep a km log"),
        new("Shipping, postage & delivery",
            "Couriers, stamps, packaging, shipping client deliverables"),
        new("Repairs & maintenance",
            "Fixing equipment, computer repair"),
        new("Training & education",
            "Courses, conferences, workshops, technical books"),
        new("Bad debts",
            "Invoices you've given up on collecting (was income first)"),
        new("Other / ask accountant",
            "Not sure? Park it here and flag it at tax time"),
    };

    public static string[] Names => All.Select(c => c.Name).ToArray();

    public static string ExamplesFor(string name) =>
        All.FirstOrDefault(c => c.Name == name)?.Examples ?? "";
}
