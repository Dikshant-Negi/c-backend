# AGENTS.md

## Goal

Convert the existing console workflows into HTTP API controllers with Swagger/OpenAPI.

**Primary goals:**
1. Use as few tokens/context as possible.
2. Reuse the existing Services, Interfaces, Models, DTOs, repositories and business rules.
3. Do not redesign existing business logic.
4. Make Swagger clearly show which endpoints each role can access.
5. After implementation, provide manual Swagger testing steps.

---

## Project Rules

Read the README and existing code before changing anything.

Current architecture:

```text
Controller -> Service Interface -> Service -> Repository -> PostgreSQL
```

Controllers should coordinate HTTP requests only.

Services contain:
- authorization
- validation
- business rules
- transactions
- database operations

Do NOT move business logic into controllers.

The existing project already contains service contracts and implementations. Reuse them.

Important service mappings:

```text
IAuthenticationService       -> AuthenticationService
IEmployeeService             -> EmployeeService
ILocationService             -> LocationService
IEquipmentTypeService       -> EquipmentTypeService
IEquipmentManagementService -> EquipmentManagementService
IEquipmentService           -> EquipmentService
IBackupsService             -> BackupsService
IBackupReservationService   -> BackupReservationService
IMaintenanceService         -> MaintenanceService
ISchedulingService          -> SchedulingService
IReportsService             -> ReportsService
```

These mappings are documented in the README. Do not create duplicate services.

---

# Token / Context Rules

## IMPORTANT

Minimize context usage.

Before editing:

1. Read `AGENTS.md`.
2. Read the relevant controller/service/interface only.
3. Do not load the entire repository unless required.
4. Do not repeatedly reread unchanged files.
5. Do not explain the entire architecture in every response.
6. Make small changes and verify them.
7. Avoid unrelated refactoring.
8. Do not rewrite working services merely to make the API work.
9. Reuse existing DTOs where appropriate.
10. If a required DTO does not exist, create the smallest DTO necessary.

When working on one endpoint, inspect only:

```text
Controller
    ↓
Service Interface
    ↓
Service Implementation
    ↓
Required DTO/Model
```

Do not inspect unrelated modules unless compilation or dependency resolution requires it.

---

# Controller Rules

Create controllers under:

```text
Controllers/
```

Controllers should:

- accept HTTP input
- validate basic request shape
- obtain the authenticated user/actor
- call the existing service
- translate service results into HTTP responses
- return appropriate status codes

Controllers should NOT:

- directly query PostgreSQL
- directly use repositories
- duplicate service validation
- implement allocation logic
- implement reservation logic
- implement ticket state transitions
- implement role authorization manually when the service already handles it

Preserve existing terminology, method names and domain concepts.

---

# Authentication / Authorization

The API must support role-based access.

Roles:

```text
Admin
Approver
Technician
Staff
```

The existing service layer remains the final authorization authority.

Therefore:

- API authorization should provide an additional HTTP boundary.
- Service authorization must remain intact.
- Never remove service-level permission checks.
- Never assume Swagger role restrictions alone provide security.

Use ASP.NET Core authorization policies/roles where appropriate.

---

# Swagger Role Organization

Swagger must make role access obvious.

Use ASP.NET Core authorization metadata so Swagger/OpenAPI can display the required role for each endpoint.

Prefer:

```csharp
[Authorize(Roles = "Admin")]
```

or:

```csharp
[Authorize(Roles = "Staff")]
```

For multiple roles:

```csharp
[Authorize(Roles = "Admin,Staff")]
```

Use the actual role values already used by the application. Do not invent new role names.

Swagger should show endpoints grouped by role and clearly indicate the required role for each endpoint.

Add concise XML comments / endpoint summaries where useful.

Do NOT create a large manually maintained Swagger permission document if attributes can express the same information.

---

# Role Endpoint Planning

Use the README menus as the starting point for endpoint coverage.

## Admin

Admin functionality includes:

```text
Dashboard KPI
Employee
Equipment
Ticket
Backup allocation
Exception dashboard
Audit logs
Change password
```

Expected controller areas may include:

```text
Admin / Reports
Employee
Equipment
Ticket
Backup
Exception
Audit
Authentication
```

Do not blindly create one controller per menu item.

Group endpoints according to the existing service/domain structure.

---

## Approver

Approver functionality:

```text
Pending approvals
Review pending ticket
Ticket details/history
Close approved ticket
Change password
```

Approver must only review tickets assigned to that approver.

Do not bypass existing service checks.

---

## Technician

Technician functionality:

```text
Equipment - view only
Assigned tickets
Update ticket
Change password
```

Technicians must not access other technicians' tickets.

---

## Staff

Swagger must reproduce the Staff workflow from the existing console application.

For now, implement ONLY these Staff operations. Do not add additional Staff endpoints unless explicitly requested.

All Staff endpoints must be grouped under:

```text
Staff
```

Use:

```csharp
[Authorize(Roles = "Staff")]
```

The existing service layer remains responsible for final authorization, ownership checks, validation, business rules, transactions, and state changes.

### 1. Ticket Requests

#### Create Ticket Request

```text
POST /api/staff/tickets
```

Purpose:
- Staff creates/posts a ticket request.
- Use the existing ticket/maintenance service.
- Do not duplicate ticket business logic in the controller.

#### View My Ticket Requests

```text
GET /api/staff/tickets
```

Purpose:
- Return ticket requests created by the authenticated Staff user.
- Do not return tickets belonging to other Staff users.

### 2. Backup Requests

#### Create Backup Request

```text
POST /api/staff/backup-requests
```

Purpose:
- Staff creates/posts a backup request.
- Use the existing backup/reservation service.
- Preserve existing allocation, reservation, escalation, expiry and transaction logic.

#### List My Backup Requests

```text
GET /api/staff/backup-requests
```

Purpose:
- Return all backup requests created by the authenticated Staff user.
- Include request ID.
- Include current request/allocation status where available.
- Do not return another Staff user's requests.

The request ID returned here is used by the confirm and cancel endpoints.

#### Confirm Backup Request

```text
POST /api/staff/backup-requests/{requestId}/confirm
```

Purpose:
- Confirm/pick up the backup associated with the specified request.
- Use the existing `BackupReservationService` flow.
- Do not recreate pickup/allocation logic in the controller.

Rules:
- Request must belong to the authenticated Staff user.
- Request must be in a valid state for confirmation.
- Existing expiry rules must be respected.
- Existing allocation/transaction/concurrency rules must be preserved.

#### Cancel Backup Request

```text
POST /api/staff/backup-requests/{requestId}/cancel
```

Purpose:
- Cancel the backup request identified by `requestId`.
- Use the existing `BackupReservationService` flow.

Rules:
- Request must belong to the authenticated Staff user.
- Request must be in a valid state for cancellation.
- Existing allocation/release/expiry rules must be preserved.

### Staff Swagger Layout

Swagger should display the Staff API approximately as:

```text
Staff

  Ticket Requests
    POST /api/staff/tickets
        Create Ticket Request

    GET /api/staff/tickets
        View My Ticket Requests

  Backup Requests
    POST /api/staff/backup-requests
        Create Backup Request

    GET /api/staff/backup-requests
        List My Backup Requests

    POST /api/staff/backup-requests/{requestId}/confirm
        Confirm Backup Request

    POST /api/staff/backup-requests/{requestId}/cancel
        Cancel Backup Request
```

Do not create additional Staff endpoints unless explicitly requested.

---

# Backup Reservation Rules

The existing `BackupReservationService` is authoritative.

Do NOT recreate allocation logic in controllers.

Preserve:

- allocation
- reservation
- pickup
- return
- cancellation
- expiry
- escalation
- transaction handling
- concurrency protection
- ownership checks
- existing statuses/enums

Controllers should simply call the existing service methods.

---

# Ticket Rules

Preserve the existing ticket lifecycle:

```text
open
  ↓
in_progress
  ↓
pending_approval
  ↓
approved
  ↓
completed
```

Rejected work returns for rework.

No controller should manually change ticket status.

The service must remain responsible for:

- actor validation
- assignment
- status validation
- version validation
- evidence validation
- approval
- rejection
- closure
- transactions

---

# HTTP Status Codes

Use consistent HTTP responses.

Prefer:

```text
200 OK
201 Created
204 NoContent
400 BadRequest
401 Unauthorized
403 Forbidden
404 NotFound
409 Conflict
422 UnprocessableEntity
500 InternalServerError
```

Do not convert every service exception into `500`.

Inspect the existing domain exceptions before deciding the mapping.

---

# API Authentication

Implement authentication using the existing authentication system where possible.

Do not create a second user/password system.

Do not expose password hashes.

Do not put passwords into logs, Swagger examples, responses, or audit descriptions.

If JWT authentication is required for HTTP, integrate it with the existing employee authentication/role model rather than replacing the existing authentication service.

---

# Swagger Testing

After controllers are implemented:

1. Run the application.
2. Open Swagger.
3. Authenticate using the API login endpoint.
4. Copy the returned token if JWT is used.
5. Use Swagger's `Authorize` button.
6. Test endpoints according to role.

Do NOT test only successful requests.

For every role, manually test:

```text
1. Allowed endpoint
2. Forbidden endpoint
3. Invalid request
4. Missing resource
5. Ownership restriction
```

---

# Manual Staff Swagger Test

## Ticket

```text
1. Login as Staff.
2. Authorize Swagger with the Staff credentials/token.
3. POST /api/staff/tickets.
4. Verify the ticket is created.
5. Copy the returned ticket/request ID.
6. GET /api/staff/tickets.
7. Verify the created request appears.
8. Login as another Staff user.
9. GET /api/staff/tickets.
10. Verify the first Staff user's request is not returned.
```

## Backup

```text
1. Login as Staff.
2. POST /api/staff/backup-requests.
3. Verify a request ID is returned.
4. GET /api/staff/backup-requests.
5. Verify the request appears with its status.
6. Use the returned request ID.
7. POST /api/staff/backup-requests/{requestId}/confirm.
8. Verify the request/allocation changes to the expected state.
9. Create another valid backup request.
10. POST /api/staff/backup-requests/{requestId}/cancel.
11. Verify the request is cancelled/released according to the existing service rules.
```

## Ownership / Security

```text
1. Create a request as Staff A.
2. Login as Staff B.
3. Try to GET/confirm/cancel Staff A's request.
4. The operation must be rejected.
5. Verify Staff cannot access Admin endpoints.
6. Verify Staff cannot access Approver endpoints.
7. Verify Staff cannot access Technician endpoints.
```

---

# Manual Test Matrix

## Admin

Verify:

```text
[ ] Login
[ ] Dashboard
[ ] Employee list
[ ] Employee create/edit/delete
[ ] Equipment operations
[ ] Ticket operations
[ ] Backup allocation
[ ] Exception dashboard
[ ] Audit logs
[ ] Change password
```

## Technician

Verify:

```text
[ ] Login
[ ] View equipment
[ ] View assigned tickets
[ ] Update assigned ticket
[ ] Add evidence
[ ] Submit ticket for approval
[ ] Change password
```

Negative tests:

```text
[ ] Technician cannot update another technician's ticket
[ ] Technician cannot approve tickets
[ ] Technician cannot access Admin operations
```

## Approver

Verify:

```text
[ ] Login
[ ] View pending approvals
[ ] Review assigned ticket
[ ] Accept ticket
[ ] Reject ticket with reason
[ ] Close approved ticket
[ ] Change password
```

Negative tests:

```text
[ ] Approver cannot review another approver's ticket
[ ] Approver cannot access Admin operations
[ ] Approver cannot act on invalid ticket state
```

---

# Build Verification

After changes:

```powershell
dotnet build EquipmentManagementBackend.csproj -c Release
```

Then run:

```powershell
dotnet run --project EquipmentManagementBackend.csproj -c Release
```

If the project has the self-test option, run:

```powershell
dotnet run --project EquipmentManagementBackend.csproj -c Release -- --self-test
```

Fix compilation errors before moving to the next controller.

---

# Change Discipline

For every requested feature:

1. Find the existing service method.
2. Find its interface method.
3. Create the smallest controller action that calls it.
4. Add role authorization.
5. Add concise Swagger documentation.
6. Build.
7. Test the endpoint.
8. Move to the next endpoint.

Do not refactor unrelated code.

Do not rename existing classes.

Do not rename existing enums.

Do not change existing service behavior unless explicitly requested.

Do not create duplicate business logic.

---

# Output After Each Task

When finished, report only:

```text
Changed:
- <files>

Endpoints:
- METHOD /route - Role

Verification:
- Build: PASS/FAIL
- Self-test: PASS/FAIL
- Swagger: PASS/FAIL

Manual checks:
1. ...
2. ...
3. ...
```

Keep responses concise. Do not repeat the project architecture unless requested.
