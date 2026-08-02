namespace PEMS.Domain.Enums;

/// <summary>
/// Which level of the configuration cascade a contact policy row belongs to. Names map 1:1 to the SQL
/// ENUM strings.
/// </summary>
public enum EmailContactScopeType
{
    /// <summary>One template, keyed by its template code. The most specific level; wins outright.</summary>
    TEMPLATE,

    /// <summary>A campus default, keyed by campus id.</summary>
    CAMPUS,

    /// <summary>A department default, keyed by department id.</summary>
    DEPARTMENT,

    /// <summary>The single fallback row. Its <c>scope_key</c> is NULL.</summary>
    SYSTEM,
}

/// <summary>
/// How badly a template needs a contact block.
///
/// <para>
/// The distinction that matters is between <see cref="OPTIONAL"/> and <see cref="REQUIRED"/>: both render
/// the block when a contact can be resolved, but only REQUIRED refuses to send when one cannot. A mail
/// whose text says "please contact the host" and shows no way to do so is the defect this exists to stop,
/// so those templates are REQUIRED and fail closed rather than shipping a dead instruction.
/// </para>
/// </summary>
public enum EmailContactRequirement
{
    /// <summary>No block. The body must not carry the placeholder either.</summary>
    NONE,

    /// <summary>Render the block if a contact resolves; send anyway if none does.</summary>
    OPTIONAL,

    /// <summary>Render the block, and refuse the send if no contact resolves.</summary>
    REQUIRED,
}

/// <summary>
/// Where the reply contact is looked up. Deliberately an enum and not a user id: a template must never be
/// able to name a specific person, because the right contact depends on the visit, the campus and the
/// moment — see <c>EmailContactResolver</c>.
/// </summary>
public enum EmailContactSource
{
    /// <summary>The Host of the specific visit instance. Never another campus's Host.</summary>
    HOST,

    /// <summary>The account that performed the action. Only where the policy says so.</summary>
    SENDER,

    /// <summary>Host first; the sender only if the visit has no usable Host.</summary>
    HOST_THEN_SENDER,

    /// <summary>The campus's own address/phone, then its IC head.</summary>
    CAMPUS_DEFAULT,

    /// <summary>The department head. Departments have no address of their own.</summary>
    DEPARTMENT_DEFAULT,

    /// <summary>The system-wide support contact from configuration.</summary>
    SUPPORT_CONTACT,
}

/// <summary>Which address, if any, the message's <c>Reply-To</c> header carries.</summary>
public enum EmailReplyToSource
{
    /// <summary>Leave the configured system Reply-To alone.</summary>
    NONE,

    /// <summary>Reply to whoever the contact block names.</summary>
    CONTACT,

    /// <summary>Reply to the account that sent it.</summary>
    SENDER,
}
