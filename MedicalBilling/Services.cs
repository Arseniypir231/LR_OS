namespace MedicalBilling;

public interface IEquipmentDepreciationService
{
    decimal GetHighTechCoefficient(string equipmentCode);
}

public interface IInsuranceCoverageService
{
    decimal GetCoveragePercentage(string insurancePolicyNumber, string serviceCode);
}

public interface IVisitHistoryService
{
    bool IsFollowUpVisit(string patientId, string visitType, DateTime currentDate);
}