namespace Rag.Evals.Corpus;

/// <summary>
/// The synthetic Northwind Systems employee handbook rendered into <c>samples/handbook.pdf</c>.
///
/// This is the single source of truth for the corpus. Three properties are load-bearing for the
/// eval and must survive any edit:
///
/// <list type="bullet">
/// <item>Every quantity is globally unique, so a lookup question has exactly one correct evidence
/// span. "thirty days" means refunds and nothing else.</item>
/// <item>Terms collide across sections on purpose ("receipt" in 2.1 and 4.2, "approval" in 4.3 and
/// 7.1, "review" in 6.3 and 11.1), so retrieval has to do real work rather than matching a term
/// that appears once.</item>
/// <item>The topics listed in <see cref="ExcludedTopics"/> appear nowhere, which is what makes the
/// unanswerable questions genuinely unanswerable.</item>
/// </list>
///
/// Gold anchors are authored from the <c>dump-text</c> output, never from this file: PDF rendering
/// and extraction round-trip whitespace, so only the extracted form is authoritative.
/// </summary>
internal static class HandbookContent
{
    public const string Title = "Northwind Systems Employee Handbook";

    /// <summary>
    /// Must appear verbatim, lowercase, and unwrapped. tests/Rag.Core.Tests/PdfParserTests.cs
    /// asserts this exact substring to prove the quickstart ingests real extracted text rather
    /// than PDF syntax, so the writer refuses to emit a handbook that breaks it across lines.
    /// </summary>
    public const string RequiredSentence = "refunds require a receipt within thirty days.";

    /// <summary>
    /// Subjects deliberately kept out of the handbook. The unanswerable questions draw from this
    /// list, and the dataset loader asserts none of these terms appear in the parsed corpus.
    /// </summary>
    public static readonly IReadOnlyList<string> ExcludedTopics =
    [
        "stock option",
        "equity vesting",
        "parental leave",
        "dental",
        "relocation",
        "visa sponsorship",
        "on-call pay",
        "sabbatical"
    ];

    public static readonly IReadOnlyList<HandbookSection> Sections =
    [
        new("1. Introduction and Scope", [
            "This handbook describes how Northwind Systems operates day to day. It covers the commitments the company makes to the people who work here and the obligations those people accept in return. Where a written contract and this handbook disagree, the contract governs.",
            "The handbook applies to permanent employees, fixed-term employees, and engaged contractors. Sections that apply to only one of those groups say so in their opening sentence. Everything else applies to everyone.",
            "Managers are responsible for making sure the people reporting to them can find and understand the policies that affect their work. Nobody is expected to memorise this document, but everybody is expected to know it exists and where to look."
        ]),
        new("1.2 How This Handbook Is Maintained", [
            "The People Operations team owns this handbook and publishes a revised edition each quarter. Substantive changes are announced in the company-wide channel before they take effect, together with a short summary of what changed and why.",
            "Anyone may propose a change by opening a request with People Operations. Proposals that affect pay, working time, or security controls are reviewed jointly with the Finance and Security teams before publication."
        ]),

        new("2. Refunds and Returns", [
            "Northwind Systems sells directly to customers and through a small number of resellers. The refund rules below apply to direct sales. Reseller transactions follow the terms in the individual reseller agreement.",
            "Policy summary: refunds require a receipt within thirty days.",
            "A customer who bought directly from us may request a full refund within thirty days of the purchase date, provided they supply the original receipt or the order confirmation email. Support agents may approve these requests themselves without escalating."
        ]),
        new("2.2 Partial Refunds and Restocking", [
            "Hardware returned in opened but undamaged condition is refunded less a restocking charge. The customer has fourteen days from delivery to start a hardware return, and the restocking charge is waived when the return is caused by a fault on our side.",
            "Software subscriptions are refunded on a pro-rata basis for the unused portion of the current billing period. Usage-based charges already incurred are never refunded, because the underlying infrastructure cost has already been paid."
        ]),
        new("2.3 Refund Disputes", [
            "A customer who disagrees with a refund decision may ask for it to be reviewed. Support must acknowledge a disputed refund within three business days and name the person who will make the final decision.",
            "Disputes that involve amounts above the agent approval limit move to the Customer Operations lead. Disputes that involve an allegation of billing fraud move immediately to Finance and are handled under the incident process in section 8 rather than the ordinary support process."
        ]),

        new("3. Customer Support Operations", [
            "Support is the front door to the company for most customers. The team's job is to resolve the problem in front of them and to make sure the rest of the company learns from what they saw.",
            "Every customer contact becomes a ticket, including contacts that arrive by phone or through an individual employee's inbox. A conversation that never became a ticket is a conversation the company cannot learn from."
        ]),
        new("3.2 Ticket Priority and Response Targets", [
            "Tickets are graded P1 through P3 at intake. P1 covers a complete loss of service for a paying customer, P2 covers degraded service or a blocked workflow with no workaround, and P3 covers everything else, including questions and feature requests.",
            "A P1 ticket must receive its first human response within four hours, measured from the moment the ticket is created rather than from the start of the next working day. P2 tickets carry a one working day target.",
            "P3 tickets must receive a first response within two business days. A P3 ticket that has been open for longer than a month without customer contact is closed automatically, and the customer is told how to reopen it."
        ]),
        new("3.3 Escalation Within Support", [
            "An agent who cannot resolve a ticket escalates it rather than holding it. Escalation is not a failure and is never recorded against an individual agent's performance.",
            "The escalation path runs from the agent to the shift lead, then to the Customer Operations lead, and then to the on-duty engineering manager. Each step must be attempted before the next one is used, except for suspected security incidents, which go straight to the Security team."
        ]),

        new("4. Expenses, Travel, and Equipment", [
            "The company reimburses reasonable costs that people incur doing their jobs. The test is whether the cost would be defensible if it were published, not whether it falls under a specific listed category.",
            "Anyone who is unsure whether a cost qualifies should ask before spending rather than after. Finance would far rather answer a question than decline a claim."
        ]),
        new("4.1 Travel Booking", [
            "Travel is booked through the company travel desk so that costs and duty-of-care obligations stay visible in one place. Bookings must be made at least twenty-one days before departure unless the trip responds to a customer incident.",
            "Economy class is the standard for all flights. Travellers on flights longer than eight hours may book premium economy where the fare difference is modest, and Finance publishes the current threshold each quarter."
        ]),
        new("4.2 Expense Reimbursement", [
            "Expenses are claimed through the finance portal. Each claim needs an itemised receipt showing the vendor, the date, and the amount, and a one-line note explaining the business purpose.",
            "Claims must be submitted within sixty days of the date the cost was incurred. Claims submitted after that window need written approval from a director, because the company's accounts for the relevant period may already have been closed.",
            "Cash advances are not issued. Anyone who cannot carry a work cost on a personal card should contact Finance, which will arrange direct billing with the vendor instead."
        ]),
        new("4.3 Equipment Purchases", [
            "Standard laptops, monitors, and peripherals are ordered from the IT catalogue and need no separate authorisation. Anything outside the catalogue is treated as a discretionary purchase.",
            "Equipment purchases above seven hundred fifty dollars require director approval before the order is placed. Splitting a single purchase into smaller orders to stay under that limit is treated as a policy breach rather than a clerical error.",
            "Equipment bought for a specific project remains company property when the project ends. The project lead is responsible for returning it to the IT pool."
        ]),

        new("5. Working Hours and Time Off", [
            "Northwind Systems does not measure people by hours logged. It does need enough predictability for teams that span time zones to work together, which is what the core hours rule below provides.",
            "Anyone whose personal circumstances make the standard pattern difficult should raise it with their manager. Adjustments are common and do not require a formal process."
        ]),
        new("5.2 Annual Leave", [
            "Permanent employees receive twenty-five days of paid annual leave each calendar year, in addition to public holidays. Leave is requested through the people portal and needs manager approval before travel is booked.",
            "Up to five days of unused leave may be carried into the following year and must be taken by the end of March. Leave beyond that is not paid out except where local law requires it.",
            "Sick leave is separate from annual leave and is not capped. People who are unwell are expected to stop working, and managers are expected to make that easy rather than admirable."
        ]),
        new("5.3 Public Holidays", [
            "The company observes eleven public holidays each year, set by the country in which the employee is based. The list for the coming year is published every November.",
            "Someone required to work on a public holiday because of a customer incident takes an equivalent day off at a time agreed with their manager."
        ]),
        new("5.4 Core Hours", [
            "Core hours are ten in the morning to four in the afternoon in the employee's own time zone. Meetings that include more than one time zone are scheduled inside the overlap and are recorded when no overlap exists.",
            "Outside core hours, people choose their own working pattern. Nobody is expected to answer messages outside their working hours, and nobody should infer from a message sent late that a reply is wanted late."
        ]),

        new("6. Contractors and Vendors", [
            "The company engages contractors for specialist work and short-term capacity. Contractors are not employees, and treating them as employees creates legal risk for both sides.",
            "Every engagement needs a written statement of work that describes the deliverable, the duration, and the rate before any work starts."
        ]),
        new("6.1 Contractor Engagement", [
            "An initial contractor engagement may run for at most ninety days. Extending beyond that requires a fresh statement of work and a review by People Operations, which checks that the engagement has not drifted into an employment relationship in all but name.",
            "Contractors work through their own equipment unless the statement of work says otherwise. Where company equipment is issued, it is tracked in the same asset register used for employees."
        ]),
        new("6.2 Contractor Invoice Payment", [
            "Contractors invoice monthly in arrears against the statement of work. Invoices are settled on net forty-five day terms from the date Finance receives a valid invoice.",
            "An invoice that does not reference a valid statement of work is returned rather than held. Finance tells the contractor and the engaging manager on the same day, so that a rejected invoice never becomes a silent delay."
        ]),
        new("6.3 Vendor Review", [
            "Every vendor that processes company or customer data is reviewed before the first contract and then on a recurring cycle. The review covers security posture, financial stability, and whether the service is still needed.",
            "The recurring vendor review runs every eighteen months. A vendor that fails a review is not automatically terminated, but the owning team must produce a remediation plan before the contract is renewed."
        ]),

        new("7. Approvals and Delegation", [
            "Approval limits exist so that spending decisions are made by someone accountable for the budget they affect, and so that no single person can commit the company alone.",
            "An approval is a decision, not a formality. Anyone asked to approve something they do not understand should decline and ask for an explanation instead."
        ]),
        new("7.1 Approval Thresholds", [
            "Team leads may approve committed spend up to one thousand dollars. Above that, approval passes upward: directors hold the next threshold, and anything larger goes to the executive team.",
            "Director approval is required for any commitment above five thousand dollars, and for any agreement that lasts longer than one year regardless of value. Multi-year commitments always go to the executive team even when the annual figure is small.",
            "Approvals are recorded in the finance portal against the commitment they authorise. A verbal approval is not an approval."
        ]),
        new("7.2 Delegation During Absence", [
            "Anyone holding an approval threshold names a delegate before a planned absence. The delegate holds the same threshold, not a reduced one, because a lower limit simply pushes decisions upward and stalls them.",
            "Delegation must be recorded at least two weeks before a planned absence begins. For unplanned absence, the absent person's manager holds the threshold until a delegate is named."
        ]),

        new("8. Incident Management", [
            "An incident is any unplanned event that degrades a service, exposes data, or puts the company at legal or financial risk. Incidents are handled by a standing process rather than by whoever happens to notice.",
            "Declaring an incident is always acceptable. A declaration that turns out to be unnecessary costs an hour; a delayed declaration can cost far more."
        ]),
        new("8.1 Incident Severity", [
            "Incidents are graded Sev1 to Sev3. Sev1 means customer data is exposed or a service is entirely unavailable, Sev2 means significant degradation with a workaround, and Sev3 means limited impact contained to internal systems.",
            "A Sev1 incident must be acknowledged by the on-duty responder within fifteen minutes of being raised. If no acknowledgement arrives in that window, the alert escalates automatically to the engineering manager on duty."
        ]),
        new("8.2 Incident Communication", [
            "Every incident has one incident lead, who owns communication and is explicitly not expected to also perform the technical remediation. Separating those roles is what keeps updates flowing while the work continues.",
            "During an active Sev1 or Sev2, the incident lead publishes a status update every one hour, even when the update is that nothing has changed. Silence is read by customers as an absence of progress.",
            "Customer-facing communication during an incident goes out through the Customer Operations lead. Individual engineers do not brief customers directly, because partial information from a credible source is harder to correct than no information."
        ]),
        new("8.3 Postmortems", [
            "Every Sev1 and every Sev2 gets a written postmortem. Postmortems are blameless: they describe what the system allowed to happen, not who typed the command.",
            "The postmortem must be published within five business days of the incident being resolved. Each action item carries a named owner and a date, and unowned action items are not accepted."
        ]),

        new("9. Information Security and Access", [
            "Security is part of everyone's job rather than the Security team's exclusive property. The team sets the controls; everyone else is expected to work inside them and to say something when a control gets in the way of legitimate work.",
            "Reporting a suspected security problem never carries a penalty, including when the person reporting it caused it."
        ]),
        new("9.1 Account Provisioning", [
            "Accounts are created from the role definition attached to the employment or engagement record, so that access follows the role rather than being assembled by request.",
            "New starter accounts are provisioned within one business day of the start date being confirmed. Access beyond the standard role bundle needs the approval of the system owner and is reviewed when the person changes role."
        ]),
        new("9.2 Authentication", [
            "Multi-factor authentication is mandatory on every company system that supports it, with no exceptions for seniority or convenience. Hardware keys are issued on request and are the preferred second factor.",
            "Passwords must be at least sixteen characters long. The company does not require periodic password rotation, because forced rotation reliably produces weaker and more predictable passwords."
        ]),
        new("9.3 Data Classification and Retention", [
            "Data is classified as public, internal, confidential, or restricted. Restricted covers customer personal data, payment details, and anything under a contractual confidentiality obligation.",
            "Restricted data is retained for seven years and then deleted, unless a legal hold applies. Retention periods for the other classifications are set by the owning team and recorded in the data register."
        ]),

        new("10. Remote Work and Devices", [
            "Northwind Systems is a distributed company with offices in three cities. Most roles can be performed from anywhere in a supported country, and the policy below describes the exceptions.",
            "Working from a country where the company has no legal entity creates tax and employment exposure, so extended work from an unsupported country needs written agreement from People Operations in advance."
        ]),
        new("10.1 Office Attendance", [
            "Employees based within commuting distance of an office attend in person at least two days per week. Which days are chosen by the team rather than by the company, so that attendance produces actual overlap.",
            "Employees outside commuting distance are fully remote and are funded to travel to a team gathering twice a year."
        ]),
        new("10.2 Device Refresh", [
            "Every employee is issued a laptop appropriate to their role. Specification requests above the standard build are handled as equipment purchases under section 4.",
            "Laptops are replaced on a thirty-six month cycle, or sooner where a fault makes the machine unfit for use. Replaced machines are wiped to the standard used for restricted data and then either redeployed or recycled."
        ]),
        new("10.3 Home Office Stipend", [
            "New employees receive a one-off home office stipend of four hundred dollars to buy a chair, a desk, or other equipment for the space they work in. The stipend is claimed through the finance portal like any other expense.",
            "Items bought with the stipend belong to the employee and are not returned at the end of employment."
        ]),

        new("11. Performance and Development", [
            "Performance conversations happen continuously. The formal cycle described below exists to make sure nothing important is left unsaid, not to replace the ordinary conversation between a person and their manager.",
            "Nobody should learn something new about their own performance for the first time in a formal review. A review that contains a surprise is a failure of the preceding months."
        ]),
        new("11.1 Review Cycle", [
            "The formal performance review runs every six months. Each review covers the work delivered, how it was delivered, and what the person wants to do next.",
            "Compensation decisions are made once a year and are informed by both reviews in the preceding period. Separating the two conversations keeps the development discussion honest."
        ]),
        new("11.2 Training Budget", [
            "Each employee has an annual development budget of twelve hundred dollars, which covers conference fees, online courses, books, and professional membership. The budget is per calendar year and does not carry over.",
            "Time spent on approved training is working time. Managers approve the time as they would approve any other work commitment, and a training request is declined only when the timing conflicts with a delivery the person owns."
        ]),

        new("12. Leaving the Company", [
            "People leave, and the company's job is to make that orderly rather than awkward. The steps below apply whether the departure is a resignation, the end of a fixed term, or a dismissal.",
            "Departing employees are asked to take part in an exit conversation with People Operations. Participation is voluntary and what is said is summarised anonymously."
        ]),
        new("12.1 Notice Periods", [
            "Permanent employees give four weeks of written notice, and the company gives the same. Notice periods for senior roles are set in the individual contract and may be longer.",
            "The company may ask someone to stop working during their notice period while continuing to pay them. That decision rests with People Operations and is not a disciplinary measure."
        ]),
        new("12.2 Asset Return", [
            "Company equipment is returned on or before the final working day, using the prepaid shipping arrangement provided by IT for remote employees.",
            "Any asset not returned within ten business days of the final working day is recorded as outstanding, and Finance contacts the former employee directly to arrange return."
        ]),
        new("12.3 Final Pay", [
            "Final pay covers salary to the last working day plus any accrued and untaken annual leave for the current year. It is paid within twenty-eight days of the final working day.",
            "Outstanding expense claims must be submitted before the final working day. Claims arriving afterwards are still honoured but are processed on the ordinary claims cycle rather than with final pay."
        ])
    ];
}
