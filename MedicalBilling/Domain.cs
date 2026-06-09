namespace MedicalBilling;

public enum DoctorCategory
{
    General,
    Specialist,
    Professor
}

public enum UrgencyLevel
{
    Scheduled,
    Urgent,
    Emergency
}

public sealed class MedicalTest
{
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}

public sealed record MedicalVisitRequest
{
    public string VisitType { get; init; } = string.Empty;

    public DoctorCategory DoctorCategory { get; init; }

    public List<MedicalTest> LabTests { get; init; } = [];

    public HashSet<string> Researches { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool UseHighTechEquipment { get; init; }

    public int Age { get; init; }

    public string InsuranceType { get; init; } = string.Empty;

    public string InsurancePolicyNumber { get; init; } = string.Empty;

    public UrgencyLevel Urgency { get; init; }

    public bool IsRepeatVisit { get; init; }

    public string PatientId { get; init; } = string.Empty;

    public DateTime CurrentDate { get; init; } = DateTime.UtcNow;
}

public sealed class MedicalInvoice
{
    public decimal VisitCost { get; init; }

    public decimal LabTestsCost { get; init; }

    public decimal ResearchCost { get; init; }

    public decimal SubtotalBeforeInsuranceAndDiscount { get; init; }

    public decimal TotalCost { get; init; }

    public decimal AgeCoefficient { get; init; }

    public decimal UrgencyCoefficient { get; init; }

    public decimal InsuranceCoefficient { get; init; }

    public decimal RepeatVisitDiscount { get; init; }

    public decimal EquipmentCoefficient { get; init; }
}