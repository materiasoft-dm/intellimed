SET NOCOUNT ON;
SELECT TOP 100
    c.FirstName, c.Surname, c.MiddleName, c.KnownAs, c.Title, c.Gender, c.DOB,
    c.Address1, c.Address2, c.Suburb, c.State, c.Postcode,
    c.PostalAddress1, c.PostalAddress2, c.PostalSuburb, c.PostalState, c.PostalPostcode,
    c.Email, c.HomePhone, c.WorkPhone, c.Mobile, c.Fax,
    c.CanSMS, c.CanEmail, c.AcceptSMSMarketing, c.Note, c.UR_NO,
    p.WarningMessage, p.FileNum, p.Type, p.ATSI,
    p.MedicareNum, p.MedicareRefNum, p.MedicareExpiryDate,
    p.VeteranNum, p.VeteranExpiryDate,
    p.PensionCode, p.PensionExpireDate, p.SafetyNetNo,
    p.FundNumber, p.FundRef, p.AliasFirstName, p.AliasSurname,
    p.IsAcceptOnlineAppointment, p.DateDeceased, p.LifeCardNum,
    p.IHINumber, p.IHIRecordStatus, p.IHIAssignedDate, p.IHINumberStatus, p.IHIUnresolvedDate,
    p.InterpreterRequired, p.PreferredLanguage
FROM Patients p
INNER JOIN Contacts c ON c.GUID = p.ContactGUID
WHERE c.DeletedDate IS NULL
  AND c.FirstName IS NOT NULL AND c.Surname IS NOT NULL
  AND c.DOB IS NOT NULL
ORDER BY c.CreatedDate DESC
FOR JSON PATH;
