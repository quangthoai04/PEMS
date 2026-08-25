namespace PEMS.ArchitectureTests;

/// <summary>
/// Pins the exact distinction <see cref="CodeReferenceScanner"/> exists for (plan §A7): a documentation
/// mention and a plain comment mention must both be ignored, while a real code reference — qualified or
/// not — must still be caught. Without this, a future edit to the scanner could silently go back to
/// plain substring matching and nobody would notice until the next false positive.
/// </summary>
public class CodeReferenceScannerTests
{
    private const string Target = "MinScheduleLeadHours";

    [Fact]
    public void Ignores_a_plain_line_comment_mention()
    {
        const string source = "// TODO: does this still use MinScheduleLeadHours?\nclass C { }";
        Assert.False(CodeReferenceScanner.ReferencesIdentifier(source, Target));
    }

    [Fact]
    public void Ignores_a_block_comment_mention()
    {
        const string source = "/* MinScheduleLeadHours was considered here and rejected */\nclass C { }";
        Assert.False(CodeReferenceScanner.ReferencesIdentifier(source, Target));
    }

    [Fact]
    public void Ignores_an_xml_doc_cref_mention()
    {
        const string source = """
            /// <summary>
            /// Exempts a caller from the 72h floor
            /// (<see cref="PEMS.Domain.Policies.VisitMutationPolicy.MinScheduleLeadHours"/>).
            /// </summary>
            public interface IFoo
            {
                void Bar(bool allowShortNoticeCreate);
            }
            """;
        Assert.False(CodeReferenceScanner.ReferencesIdentifier(source, Target));
    }

    [Fact]
    public void Detects_a_real_qualified_member_access_reference()
    {
        const string source = """
            class C
            {
                void M()
                {
                    var hours = VisitMutationPolicy.MinScheduleLeadHours;
                }
            }
            """;
        Assert.True(CodeReferenceScanner.ReferencesIdentifier(source, Target));
    }

    [Fact]
    public void Detects_a_real_unqualified_reference()
    {
        const string source = """
            class C
            {
                void M()
                {
                    int hours = MinScheduleLeadHours;
                }
            }
            """;
        Assert.True(CodeReferenceScanner.ReferencesIdentifier(source, Target));
    }

    [Fact]
    public void Ignores_a_file_that_never_mentions_the_identifier_at_all()
    {
        const string source = "class C { void M() { var hours = VisitMutationPolicy.RequiredLeadHours; } }";
        Assert.False(CodeReferenceScanner.ReferencesIdentifier(source, Target));
    }
}
