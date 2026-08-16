using System.Net;

namespace eTicketing.Notifications.Email.Templates;

/// <summary>Shared inline-CSS HTML shell every template wraps its body content in — keeps the
/// 5 template files focused on their own content instead of each re-declaring the same
/// boilerplate. Deliberately plain string interpolation (no Razor/templating engine), matching
/// this project's "plain C# string templates" design decision.</summary>
internal static class EmailLayout
{
    public static string Wrap(string title, string bodyHtml) => $$"""
        <!doctype html>
        <html lang="bs">
        <!-- title is also used verbatim as the real email Subject header by callers (see each
             *Template.Render) — only this <title> tag occurrence is HTML-encoded, not the value
             callers return, so the actual Subject header never picks up literal &amp;-entities. -->
        <head><meta charset="utf-8"><title>{{WebUtility.HtmlEncode(title)}}</title></head>
        <body style="margin:0;padding:0;background-color:#f4f4f7;font-family:Segoe UI,Arial,sans-serif;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f4f4f7;padding:32px 0;">
            <tr>
              <td align="center">
                <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color:#ffffff;border-radius:8px;overflow:hidden;">
                  <tr>
                    <td style="background-color:#1f2937;padding:20px 32px;">
                      <span style="color:#ffffff;font-size:20px;font-weight:700;">eKarta</span>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:32px;color:#1f2937;font-size:15px;line-height:1.6;">
                      {{bodyHtml}}
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:16px 32px;background-color:#f9fafb;color:#9ca3af;font-size:12px;">
                      Ovo je automatska poruka sa eKarta platforme — nemojte odgovarati na ovaj email.
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;
}
