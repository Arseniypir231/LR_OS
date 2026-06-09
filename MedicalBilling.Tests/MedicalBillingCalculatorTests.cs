using Moq;

namespace MedicalBilling.Tests;

public sealed class MedicalBillingCalculatorTests
{
    [Fact]
    public void CalculateInvoice_ValidBasicScenario_ReturnsExpectedInvoice()
    {
        // Arrange
        var equipmentService = new Mock<IEquipmentDepreciationService>();
        equipmentService
            .Setup(x => x.GetHighTechCoefficient(It.IsAny<string>()))
            .Returns(1.1m);

        var insuranceService = new Mock<IInsuranceCoverageService>();
        var visitHistoryService = new Mock<IVisitHistoryService>();

        var sut = new MedicalBillingCalculator(
            equipmentService.Object,
            insuranceService.Object,
            visitHistoryService.Object);

        var request = CreateValidRequest() with
        {
            VisitType = "Consultation",
            DoctorCategory = DoctorCategory.General,
            Age = 30,
            Urgency = UrgencyLevel.Scheduled,
            InsuranceType = "None",
            IsRepeatVisit = false,
            UseHighTechEquipment = false,
            LabTests = [new MedicalTest { Code = "CBC", Name = "Blood count" }],
            Researches = new HashSet<string>()
        };

        // Act
        MedicalInvoice actual = sut.CalculateInvoice(request);

        // Assert
        Assert.Equal(2000m, actual.VisitCost);
        Assert.Equal(800m, actual.LabTestsCost);
        Assert.Equal(0m, actual.ResearchCost);
        Assert.Equal(2800m, actual.SubtotalBeforeInsuranceAndDiscount);
        Assert.Equal(2800m, actual.TotalCost);
        Assert.Equal(1.0m, actual.AgeCoefficient);
        Assert.Equal(1.0m, actual.UrgencyCoefficient);
        Assert.Equal(1.0m, actual.InsuranceCoefficient);
        Assert.Equal(0m, actual.RepeatVisitDiscount);
        Assert.Equal(1.0m, actual.EquipmentCoefficient);
    }

    [Theory]
    [InlineData(0, 0.9)]
    [InlineData(17, 0.9)]
    [InlineData(18, 1.0)]
    [InlineData(65, 1.0)]
    [InlineData(66, 1.1)]
    [InlineData(120, 1.1)]
    public void CalculateInvoice_AgeBoundaries_AppliesExpectedAgeCoefficient(int age, decimal expectedAgeCoefficient)
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateValidRequest() with
        {
            Age = age,
            InsuranceType = "None",
            LabTests = [],
            Researches = new HashSet<string>()
        };

        // Act
        MedicalInvoice actual = sut.CalculateInvoice(request);

        // Assert
        Assert.Equal(expectedAgeCoefficient, actual.AgeCoefficient);
    }

    [Theory]
    [InlineData(UrgencyLevel.Scheduled, 1.0)]
    [InlineData(UrgencyLevel.Urgent, 1.3)]
    [InlineData(UrgencyLevel.Emergency, 1.8)]
    public void CalculateInvoice_UrgencyVariants_AppliesExpectedUrgencyCoefficient(
        UrgencyLevel urgency,
        decimal expectedUrgencyCoefficient)
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateValidRequest() with
        {
            Urgency = urgency,
            InsuranceType = "None",
            LabTests = [],
            Researches = new HashSet<string>()
        };

        // Act
        MedicalInvoice actual = sut.CalculateInvoice(request);

        // Assert
        Assert.Equal(expectedUrgencyCoefficient, actual.UrgencyCoefficient);
    }

    [Theory]
    [InlineData("Consultation", DoctorCategory.General, 2000)]
    [InlineData("Diagnostics", DoctorCategory.Specialist, 4550)]
    [InlineData("Procedure", DoctorCategory.General, 3600)]
    [InlineData("Checkup", DoctorCategory.Professor, 15000)]
    public void CalculateInvoice_VisitTypeAndDoctorCategory_CalculatesVisitCost(
        string visitType,
        DoctorCategory doctorCategory,
        decimal expectedVisitCost)
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateValidRequest() with
        {
            VisitType = visitType,
            DoctorCategory = doctorCategory,
            InsuranceType = "None",
            LabTests = [],
            Researches = new HashSet<string>()
        };

        // Act
        MedicalInvoice actual = sut.CalculateInvoice(request);

        // Assert
        Assert.Equal(expectedVisitCost, actual.VisitCost);
    }

    [Fact]
    public void CalculateInvoice_UnknownLabTestCode_UsesDefaultPrice()
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateValidRequest() with
        {
            InsuranceType = "None",
            LabTests =
            [
                new MedicalTest { Code = "UNKNOWN", Name = "Unknown test" }
            ],
            Researches = new HashSet<string>()
        };

        // Act
        MedicalInvoice actual = sut.CalculateInvoice(request);

        // Assert
        Assert.Equal(500m, actual.LabTestsCost);
    }

    [Fact]
    public void CalculateInvoice_HighTechResearch_AppliesEquipmentCoefficient()
    {
        // Arrange
        var equipmentService = new Mock<IEquipmentDepreciationService>();
        equipmentService
            .Setup(x => x.GetHighTechCoefficient("HIGH_TECH"))
            .Returns(1.1m);
        var insuranceService = new Mock<IInsuranceCoverageService>();
        var visitHistoryService = new Mock<IVisitHistoryService>();
        var sut = new MedicalBillingCalculator(
            equipmentService.Object,
            insuranceService.Object,
            visitHistoryService.Object);

        var request = CreateValidRequest() with
        {
            InsuranceType = "None",
            UseHighTechEquipment = true,
            LabTests = [],
            Researches = new HashSet<string> { "MRI", "XRay" }
        };

        // Act
        MedicalInvoice actual = sut.CalculateInvoice(request);

        // Assert
        Assert.Equal(10450m, actual.ResearchCost);
        Assert.Equal(1.1m, actual.EquipmentCoefficient);
        equipmentService.Verify(x => x.GetHighTechCoefficient("HIGH_TECH"), Times.Once);
    }

    [Fact]
    public void CalculateInvoice_ResearchWithoutHighTech_DoesNotCallEquipmentService()
    {
        // Arrange
        var equipmentService = new Mock<IEquipmentDepreciationService>();
        var insuranceService = new Mock<IInsuranceCoverageService>();
        var visitHistoryService = new Mock<IVisitHistoryService>();
        var sut = new MedicalBillingCalculator(
            equipmentService.Object,
            insuranceService.Object,
            visitHistoryService.Object);

        var request = CreateValidRequest() with
        {
            InsuranceType = "None",
            UseHighTechEquipment = false,
            LabTests = [],
            Researches = new HashSet<string> { "MRI" }
        };

        // Act
        MedicalInvoice actual = sut.CalculateInvoice(request);

        // Assert
        Assert.Equal(8000m, actual.ResearchCost);
        Assert.Equal(1.0m, actual.EquipmentCoefficient);
        equipmentService.Verify(x => x.GetHighTechCoefficient(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("None", 1.0)]
    [InlineData("Basic", 0.7)]
    public void CalculateInvoice_FixedInsuranceTypes_ApplyExpectedCoefficient(string insuranceType, decimal expected)
    {
        // Arrange
        var insuranceService = new Mock<IInsuranceCoverageService>();
        var sut = CreateSut(insuranceService: insuranceService);
        var request = CreateValidRequest() with
        {
            InsuranceType = insuranceType,
            LabTests = [],
            Researches = new HashSet<string>()
        };

        // Act
        MedicalInvoice actual = sut.CalculateInvoice(request);

        // Assert
        Assert.Equal(expected, actual.InsuranceCoefficient);
        insuranceService.Verify(
            x => x.GetCoveragePercentage(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Theory]
    [InlineData("Corporate")]
    [InlineData("VHI")]
    public void CalculateInvoice_InsuranceRequiringCoverage_CallsExternalService(string insuranceType)
    {
        // Arrange
        var insuranceService = new Mock<IInsuranceCoverageService>();
        insuranceService
            .Setup(x => x.GetCoveragePercentage("POL123", It.IsAny<string>()))
            .Returns(0.5m);
        var sut = CreateSut(insuranceService: insuranceService);
        var request = CreateValidRequest() with
        {
            InsuranceType = insuranceType,
            InsurancePolicyNumber = "POL123",
            LabTests = [],
            Researches = new HashSet<string>()
        };

        // Act
        MedicalInvoice actual = sut.CalculateInvoice(request);

        // Assert
        Assert.Equal(0.5m, actual.InsuranceCoefficient);
        insuranceService.Verify(
            x => x.GetCoveragePercentage("POL123", It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public void CalculateInvoice_RepeatVisitAndConfirmedFollowUp_AppliesDiscount()
    {
        // Arrange
        var visitHistoryService = new Mock<IVisitHistoryService>();
        visitHistoryService
            .Setup(x => x.IsFollowUpVisit("P-1", "Consultation", It.IsAny<DateTime>()))
            .Returns(true);
        var sut = CreateSut(visitHistoryService: visitHistoryService);
        var request = CreateValidRequest() with
        {
            PatientId = "P-1",
            IsRepeatVisit = true,
            InsuranceType = "None",
            LabTests = [],
            Researches = new HashSet<string>()
        };

        // Act
        MedicalInvoice actual = sut.CalculateInvoice(request);

        // Assert
        Assert.Equal(0.2m, actual.RepeatVisitDiscount);
        Assert.Equal(1600m, actual.TotalCost);
        visitHistoryService.Verify(
            x => x.IsFollowUpVisit("P-1", "Consultation", It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public void CalculateInvoice_RepeatFlagFalse_DoesNotCallVisitHistoryService()
    {
        // Arrange
        var visitHistoryService = new Mock<IVisitHistoryService>();
        var sut = CreateSut(visitHistoryService: visitHistoryService);
        var request = CreateValidRequest() with
        {
            IsRepeatVisit = false,
            InsuranceType = "None",
            LabTests = [],
            Researches = new HashSet<string>()
        };

        // Act
        MedicalInvoice actual = sut.CalculateInvoice(request);

        // Assert
        Assert.Equal(0m, actual.RepeatVisitDiscount);
        visitHistoryService.Verify(
            x => x.IsFollowUpVisit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(121)]
    public void CalculateInvoice_AgeOutOfRange_ThrowsArgumentOutOfRangeException(int badAge)
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateValidRequest() with { Age = badAge };

        // Act
        Action act = () => sut.CalculateInvoice(request);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("UnknownVisit")]
    public void CalculateInvoice_InvalidVisitType_ThrowsArgumentException(string badVisitType)
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateValidRequest() with { VisitType = badVisitType };

        // Act
        Action act = () => sut.CalculateInvoice(request);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Gold")]
    public void CalculateInvoice_InvalidInsuranceType_ThrowsArgumentException(string insuranceType)
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateValidRequest() with { InsuranceType = insuranceType };

        // Act
        Action act = () => sut.CalculateInvoice(request);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void CalculateInvoice_LabTestWithEmptyCode_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateValidRequest() with
        {
            InsuranceType = "None",
            LabTests = [new MedicalTest { Code = "", Name = "Invalid" }]
        };

        // Act
        Action act = () => sut.CalculateInvoice(request);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void CalculateInvoice_ResearchWithWhitespaceCode_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateValidRequest() with
        {
            InsuranceType = "None",
            LabTests = [],
            Researches = new HashSet<string> { " " }
        };

        // Act
        Action act = () => sut.CalculateInvoice(request);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void CalculateInvoice_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        Action act = () => sut.CalculateInvoice(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void CalculateInvoice_NullLabTests_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateValidRequest() with { LabTests = null! };

        // Act
        Action act = () => sut.CalculateInvoice(request);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void CalculateInvoice_NullResearches_ThrowsArgumentNullException()
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateValidRequest() with { Researches = null! };

        // Act
        Action act = () => sut.CalculateInvoice(request);

        // Assert
        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void CalculateInvoice_LabTestsContainsNull_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();
        var list = new List<MedicalTest> { null! };
        var request = CreateValidRequest() with
        {
            InsuranceType = "None",
            LabTests = list
        };

        // Act
        Action act = () => sut.CalculateInvoice(request);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void CalculateInvoice_InvalidUrgency_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateValidRequest() with
        {
            Urgency = (UrgencyLevel)999,
            InsuranceType = "None"
        };

        // Act
        Action act = () => sut.CalculateInvoice(request);

        // Assert
        Assert.Throws<ArgumentOutOfRangeException>(act);
    }

    [Fact]
    public void CalculateInvoice_InsuranceTypeNull_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateValidRequest() with { InsuranceType = null! };

        // Act
        Action act = () => sut.CalculateInvoice(request);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void CalculateInvoice_VisitTypeNull_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateValidRequest() with { VisitType = null! };

        // Act
        Action act = () => sut.CalculateInvoice(request);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void CalculateInvoice_RepeatVisitNotConfirmed_NoDiscount()
    {
        // Arrange
        var visitHistoryService = new Mock<IVisitHistoryService>();
        visitHistoryService
            .Setup(x => x.IsFollowUpVisit("P-1", "Consultation", It.IsAny<DateTime>()))
            .Returns(false);
        var sut = CreateSut(visitHistoryService: visitHistoryService);
        var request = CreateValidRequest() with
        {
            PatientId = "P-1",
            IsRepeatVisit = true,
            InsuranceType = "None",
            LabTests = [],
            Researches = new HashSet<string>()
        };

        // Act
        MedicalInvoice actual = sut.CalculateInvoice(request);

        // Assert
        Assert.Equal(0m, actual.RepeatVisitDiscount);
        Assert.Equal(2000m, actual.TotalCost);
        visitHistoryService.Verify(
            x => x.IsFollowUpVisit("P-1", "Consultation", It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public void CalculateInvoice_UnknownResearchCode_UsesDefaultBasePrice()
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateValidRequest() with
        {
            InsuranceType = "None",
            LabTests = [],
            Researches = new HashSet<string> { "UnknownScan" }
        };

        // Act
        MedicalInvoice actual = sut.CalculateInvoice(request);

        // Assert
        Assert.Equal(1000m, actual.ResearchCost);
    }

    [Fact]
    public void CalculateInvoice_LabTestWithWhitespaceOnlyCode_ThrowsArgumentException()
    {
        // Arrange
        var sut = CreateSut();
        var request = CreateValidRequest() with
        {
            InsuranceType = "None",
            LabTests = [new MedicalTest { Code = "   ", Name = "Spaces" }]
        };

        // Act
        Action act = () => sut.CalculateInvoice(request);

        // Assert
        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void CalculateInvoice_InsuranceTypeTrimmed_AppliesBasic()
    {
        // Arrange
        var insuranceService = new Mock<IInsuranceCoverageService>();
        var sut = CreateSut(insuranceService: insuranceService);
        var request = CreateValidRequest() with
        {
            InsuranceType = "  Basic  ",
            LabTests = [],
            Researches = new HashSet<string>()
        };

        // Act
        MedicalInvoice actual = sut.CalculateInvoice(request);

        // Assert
        Assert.Equal(0.7m, actual.InsuranceCoefficient);
        insuranceService.Verify(
            x => x.GetCoveragePercentage(It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    private static MedicalBillingCalculator CreateSut(
        Mock<IEquipmentDepreciationService>? equipmentService = null,
        Mock<IInsuranceCoverageService>? insuranceService = null,
        Mock<IVisitHistoryService>? visitHistoryService = null)
    {
        var equipment = equipmentService ?? new Mock<IEquipmentDepreciationService>();
        if (equipmentService is null)
        {
            equipment.Setup(x => x.GetHighTechCoefficient(It.IsAny<string>())).Returns(1.1m);
        }

        var insurance = insuranceService ?? new Mock<IInsuranceCoverageService>();
        if (insuranceService is null)
        {
            insurance.Setup(x => x.GetCoveragePercentage(It.IsAny<string>(), It.IsAny<string>())).Returns(0.6m);
        }

        var history = visitHistoryService ?? new Mock<IVisitHistoryService>();
        if (visitHistoryService is null)
        {
            history.Setup(x => x.IsFollowUpVisit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>())).Returns(false);
        }

        return new MedicalBillingCalculator(equipment.Object, insurance.Object, history.Object);
    }

    private static MedicalVisitRequest CreateValidRequest()
    {
        return new MedicalVisitRequest
        {
            VisitType = "Consultation",
            DoctorCategory = DoctorCategory.General,
            LabTests = [],
            Researches = new HashSet<string>(),
            UseHighTechEquipment = false,
            Age = 30,
            InsuranceType = "None",
            InsurancePolicyNumber = "POL-001",
            Urgency = UrgencyLevel.Scheduled,
            IsRepeatVisit = false,
            PatientId = "P-1",
            CurrentDate = new DateTime(2026, 4, 28)
        };
    }
}
