namespace InsuranceExtraction.Domain.Enums;

public enum SubmissionStatus
{
    Pending,
    Processed,
    Failed,
    NeedsReview
}

public enum LineOfBusiness
{
    AutoLiability,
    GeneralLiability,
    WorkersCompensation,
    Property,
    Umbrella,
    Cargo,
    PhysicalDamage,
    MotorTruckCargo,
    NonTruckingLiability,
    Other
}

public enum ExposureType
{
    Trucks,
    PowerUnits,
    Tractors,
    Trailers,
    Drivers,
    Miles,
    AnnualMiles,
    Payroll,
    Revenue,
    Locations,
    Employees,
    Radius,
    States,
    Other
}
