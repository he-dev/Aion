
Information logs emitted through activity statuses are contractual telemetry. They are expected to be complete, structured, and observable. Missing required fields are programming errors and should fail fast. Debug and Trace are non-contractual diagnostics and may remain freeform.


Level
Meaning in your framework
Information
Contractual observable facts
Debug
Developer diagnostics
Trace
Freestyle low-level diagnostics
Error
Contractual failure status or exceptional diagnostic


Info = facts you intend to observe
Debug/Trace = commentary while developing/investigating
Error = either an observed failed contract status or an exceptional condition

----

## Theoretical premises

This framework treats observability as a **contract**, not as ordinary logging. Ordinary logs describe what the developer wants to say at a particular call site. Contract telemetry describes what the system promises to make observable in a structured and consistent way.

The central unit of observation is an **activity**. An activity represents a named contract that the program executes. Every activity must end with exactly one **status**. A status records whether the activity ran according to the programmed contract and attaches the structured data needed to understand that outcome.

`Ok` does not mean that the business outcome was positive. It means the activity completed along an expected coded path. For example, finding zero records, choosing a fallback path, or rejecting invalid input can all be `Ok` if those are programmed outcomes. `Error` is reserved for cases where the activity could not complete its contract as intended.

Expected alternative paths should be represented as structured status data, such as a reason, outcome, count, selected mode, or other contract-specific fields. If a sub-operation is important enough to observe independently, it should be modeled as its own activity with its own status, even if it has no meaningful duration. The framework does not use events as a separate observability concept; an event is considered an underspecified form of an activity status.

The framework distinguishes contractual telemetry from diagnostic logging. Information-level telemetry belongs to the contract layer and should be emitted through typed activities and statuses. Debug and trace logs remain freeform by design, because they are diagnostic rather than contractual.

Channels classify the role of an observation. For example, one channel can describe what the system produces, while another describes the internal machinery that enables production. This gives telemetry an additional semantic dimension beyond severity level.

The framework is intentionally strict. Missing required context, logging a status for the wrong activity, completing an activity twice, or disposing an incomplete activity are programming errors. Failing fast is preferred to silently emitting incomplete or misleading observability data.

Note: A single-status activity may be emitted without an explicit scope.

---

## Intended advantages

The main advantage is that important telemetry becomes **explicit, structured, and type-checked**. Instead of scattering freeform information logs through the codebase, the system defines named activities and status factories that describe the observable contract.

This improves consistency. The same activity produces the same shape of telemetry across call sites. Status messages, dimensions, counters, reasons, overloads, durations, and exceptions are centralized in the contract definitions rather than recreated manually at every log statement.

It also improves correctness. The generic activity/status relationship prevents logging a status for the wrong activity. Required context, such as an overload or outcome, is enforced instead of being silently omitted. Runtime failures in telemetry are treated as defects in the program’s observability contract.

The activity/status model makes telemetry easier to query and aggregate. Observations have stable names and common dimensions such as activity, channel, status, reason, overload, duration, and contract-specific measurements. This supports questions like which activities ran, which expected paths they took, how often they failed, how long they took, and what result values they produced.

The model avoids mixing contractual observations with diagnostic commentary. Information-level telemetry remains meaningful and stable, while debug and trace logging can still be used for exploratory or implementation-level details without weakening the contract layer.

Finally, the framework encourages deliberate modeling. If something matters operationally, it must be represented as part of an activity status or as a separate activity. If it does not matter enough to be part of the contract, it should remain diagnostic. This keeps the observable surface of the system intentional rather than accidental.

