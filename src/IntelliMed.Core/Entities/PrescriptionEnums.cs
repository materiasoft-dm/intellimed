namespace IntelliMed.Core.Entities;

public enum PrescriptionStatus
{
    Active,
    Ceased,
    Cancelled
}

/// <summary>How a PrescriptionIssue was (or will be) delivered. Paper is the only functional value
/// today — Electronic/SendToPharmacy exist so the schema doesn't need a breaking change once
/// IPrescriptionTransmissionService gets a real implementation.</summary>
public enum PrescriptionDeliveryMethod
{
    Paper,
    Electronic,
    SendToPharmacy
}

/// <summary>Orthogonal to PrescriptionStatus, same relationship Invoice.ClaimStatus has to
/// InvoiceStatus. Append-only — persisted as a plain int, never renumber. NotApplicable covers Paper
/// deliveries (nothing to transmit); nothing drives real transitions until
/// IPrescriptionTransmissionService has a real implementation, so this is manually set for now.</summary>
public enum PrescriptionTransmissionStatus
{
    NotApplicable,
    Pending,
    Sent,
    Failed
}

/// <summary>How this script came to be — mirrors legacy Medinet's DoseCtrl SOURCE_CODE. Captured for
/// the clinical record, not used to drive any workflow branching.</summary>
public enum PrescriptionSourceCode
{
    PrintedPrescription,
    HandwrittenPrescription,
    ProductAdvisedHere,
    ProductSuppliedHere,
    AdvisedOrPrescribedElsewhere
}
