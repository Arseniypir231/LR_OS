using System.Globalization;

namespace MedicalBilling;

public sealed class MedicalBillingCalculator(
    IEquipmentDepreciationService equipmentDepreciationService,
    IInsuranceCoverageService insuranceCoverageService,
    IVisitHistoryService visitHistoryService)
{
    private const decimal FollowUpVisitDiscount = 0.2m;

    private static readonly Dictionary<string, decimal> VisitTypeCoefficients = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Consultation"] = 1.0m,
        ["Diagnostics"] = 1.3m,
        ["Procedure"] = 1.8m,
        ["Checkup"] = 2.5m
    };

    private static readonly Dictionary<DoctorCategory, decimal> DoctorCategoryRates = new()
    {
        [DoctorCategory.General] = 2000m,
        [DoctorCategory.Specialist] = 3500m,
        [DoctorCategory.Professor] = 6000m
    };

    private static readonly Dictionary<string, decimal> LabTestPrices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CBC"] = 800m,
        ["Glucose"] = 300m,
        ["LipidPanel"] = 1500m
    };

    private static readonly Dictionary<string, decimal> ResearchBasePrices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MRI"] = 8000m,
        ["Ultrasound"] = 2500m,
        ["XRay"] = 1500m,
        ["ECG"] = 1200m
    };

    public MedicalInvoice CalculateInvoice(MedicalVisitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        decimal visitCost = CalculateVisitCost(request.VisitType, request.DoctorCategory);
        decimal labTestsCost = CalculateLabTestsCost(request.LabTests);
        decimal equipmentCoefficient = request.UseHighTechEquipment
            ? equipmentDepreciationService.GetHighTechCoefficient("HIGH_TECH")
            : 1.0m;
        decimal researchCost = CalculateResearchCost(request.Researches, equipmentCoefficient);

        decimal ageCoefficient = ResolveAgeCoefficient(request.Age);
        decimal urgencyCoefficient = ResolveUrgencyCoefficient(request.Urgency);
        decimal subtotal = (visitCost + labTestsCost + researchCost) * ageCoefficient * urgencyCoefficient;

        decimal insuranceCoefficient = ResolveInsuranceCoefficient(request);
        decimal repeatVisitDiscount = ResolveRepeatVisitDiscount(request);
        decimal total = subtotal * insuranceCoefficient * (1 - repeatVisitDiscount);

        return new MedicalInvoice
        {
            VisitCost = visitCost,
            LabTestsCost = labTestsCost,
            ResearchCost = researchCost,
            SubtotalBeforeInsuranceAndDiscount = subtotal,
            TotalCost = total,
            AgeCoefficient = ageCoefficient,
            UrgencyCoefficient = urgencyCoefficient,
            InsuranceCoefficient = insuranceCoefficient,
            RepeatVisitDiscount = repeatVisitDiscount,
            EquipmentCoefficient = equipmentCoefficient
        };
    }

    private static decimal CalculateVisitCost(string visitType, DoctorCategory doctorCategory)
    {
        decimal baseRate = DoctorCategoryRates[doctorCategory];
        decimal typeCoefficient = VisitTypeCoefficients[visitType];
        return baseRate * typeCoefficient;
    }

    private static decimal CalculateLabTestsCost(IEnumerable<MedicalTest> tests)
    {
        decimal sum = 0m;
        foreach (MedicalTest test in tests)
        {
            if (test is null)
            {
                throw new ArgumentException("Lab tests list contains null element.", nameof(tests));
            }

            if (string.IsNullOrWhiteSpace(test.Code))
            {
                throw new ArgumentException("Lab test code cannot be empty.", nameof(tests));
            }

            sum += LabTestPrices.TryGetValue(test.Code, out decimal testPrice) ? testPrice : 500m;
        }

        return sum;
    }

    private static decimal CalculateResearchCost(IEnumerable<string> researches, decimal equipmentCoefficient)
    {
        decimal sum = 0m;
        foreach (string research in researches)
        {
            if (string.IsNullOrWhiteSpace(research))
            {
                throw new ArgumentException("Research code cannot be empty.", nameof(researches));
            }

            decimal basePrice = ResearchBasePrices.TryGetValue(research, out decimal found) ? found : 1000m;
            sum += basePrice * equipmentCoefficient;
        }

        return sum;
    }

    private static decimal ResolveAgeCoefficient(int age)
    {
        return age switch
        {
            < 18 => 0.9m,
            <= 65 => 1.0m,
            _ => 1.1m
        };
    }

    private static decimal ResolveUrgencyCoefficient(UrgencyLevel urgency)
    {
        return urgency switch
        {
            UrgencyLevel.Scheduled => 1.0m,
            UrgencyLevel.Urgent => 1.3m,
            UrgencyLevel.Emergency => 1.8m,
            _ => throw new ArgumentOutOfRangeException(nameof(urgency), urgency, "Unsupported urgency level.")
        };
    }

    private static void ValidateRequest(MedicalVisitRequest request)
    {
        if (request.Age is < 0 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Age), "Age must be in range [0, 120].");
        }

        if (string.IsNullOrWhiteSpace(request.VisitType) || !VisitTypeCoefficients.ContainsKey(request.VisitType))
        {
            throw new ArgumentException("Visit type is invalid.", nameof(request.VisitType));
        }

        if (request.LabTests is null)
        {
            throw new ArgumentNullException(nameof(request.LabTests));
        }

        if (request.Researches is null)
        {
            throw new ArgumentNullException(nameof(request.Researches));
        }

        if (string.IsNullOrWhiteSpace(request.InsuranceType))
        {
            throw new ArgumentException("Insurance type is required.", nameof(request.InsuranceType));
        }
    }

    private decimal ResolveInsuranceCoefficient(MedicalVisitRequest request)
    {
        return request.InsuranceType.Trim() switch
        {
            "None" => 1.0m,
            "Basic" => 0.7m,
            "Corporate" or "VHI" => insuranceCoverageService.GetCoveragePercentage(
                request.InsurancePolicyNumber,
                BuildServiceCode(request)),
            _ => throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"Insurance type '{request.InsuranceType}' is unsupported."),
                nameof(request.InsuranceType))
        };
    }

    private decimal ResolveRepeatVisitDiscount(MedicalVisitRequest request)
    {
        if (!request.IsRepeatVisit)
        {
            return 0m;
        }

        bool isFollowUp = visitHistoryService.IsFollowUpVisit(request.PatientId, request.VisitType, request.CurrentDate);
        return isFollowUp ? FollowUpVisitDiscount : 0m;
    }

    private static string BuildServiceCode(MedicalVisitRequest request)
    {
        return $"{request.VisitType}:{request.DoctorCategory}:{request.Urgency}";
    }
}