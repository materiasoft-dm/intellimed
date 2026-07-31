namespace IntelliMed.Core.Utilities;

/// <summary>Resolves <c>{{Token}}</c> placeholders in an email template's subject/body against a set of merge values.</summary>
public static class EmailTemplateRenderer
{
    public static (string Subject, string Body) Render(string subject, string bodyHtml, IDictionary<string, string> tokens)
    {
        foreach (var (key, value) in tokens)
        {
            var placeholder = $"{{{{{key}}}}}";
            subject = subject.Replace(placeholder, value);
            bodyHtml = bodyHtml.Replace(placeholder, value);
        }

        return (subject, bodyHtml);
    }
}
