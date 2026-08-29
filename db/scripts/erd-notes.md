# InternLink Relational Integrity & Foreign Key Design Notes

This document details the foreign key relationships, cascade behaviors, and design rationales for the InternLink database schema (supporting CSE 3224 / CSE 3200 engineering documentation).

---

## Foreign Key Cascade Behaviors & Design Rationales

| Source Table | Foreign Key Column | Target Table | On Delete | Design Rationale |
| :--- | :--- | :--- | :--- | :--- |
| **`Students`** | `UserId` | `AspNetUsers(Id)` | `CASCADE` | Purging an identity user account cleanly removes the student profile. |
| **`Companies`** | `UserId` | `AspNetUsers(Id)` | `CASCADE` | Purging an identity user account cleanly removes the company profile. |
| **`Jobs`** | `CompanyId` | `Companies(Id)` | `CASCADE` | Deleting a company profile removes all associated job postings. |
| **`Resumes`** | `StudentId` | `Students(Id)` | `CASCADE` | Removing a student profile deletes their resume records and metadata. |
| **`Applications`** | `JobId` | `Jobs(Id)` | `NO ACTION` | **Deliberate**: Jobs are closed (`IsClosed=1`), never hard-deleted. Preserves student application history and audit trails. |
| **`Applications`** | `StudentId` | `Students(Id)` | `CASCADE` | Removing a student profile removes all of their submitted job applications. |
| **`Applications`** | `AttachedResumeId`| `Resumes(Id)` | `NO ACTION` | Retains application integrity if resume drafts are updated or modified. |
| **`Interviews`** | `ApplicationId` | `Applications(Id)` | `CASCADE` | Deleting an application removes all associated scheduled interview events. |
| **`StudentSkills`**| `StudentId` | `Students(Id)` | `CASCADE` | Deleting a student profile purges their linked skill inventory. |
| **`StudentSkills`**| `SkillId` | `Skills(Id)` | `CASCADE` | Deleting a reference skill purges all associated student skill associations. |
| **`JobSkills`** | `JobId` | `Jobs(Id)` | `CASCADE` | Deleting a job removes its weighted skill requirement associations. |
| **`JobSkills`** | `SkillId` | `Skills(Id)` | `CASCADE` | Deleting a reference skill removes the requirement from all jobs. |
| **`Notifications`**| `TargetUserId` | `AspNetUsers(Id)` | `CASCADE` | Deleting a user account deletes their entire notification inbox. |
| **`Assessments`** | `StudentId` | `Students(Id)` | `CASCADE` | Deleting a student profile cleans up their skill assessment scores. |
| **`Assessments`** | `SkillId` | `Skills(Id)` | `CASCADE` | Deleting a reference skill removes associated assessment test records. |
| **`CounselorFeedback`** | `StudentId` | `Students(Id)` | `CASCADE` | Deleting a student profile removes advising notes written for them. |
| **`CounselorFeedback`** | `CounselorUserId` | `AspNetUsers(Id)` | `NO ACTION` | **Deliberate**: Removing a counselor user preserves the student's historical advising notes. |
| **`AIHistory`** | `UserId` | `AspNetUsers(Id)` | `CASCADE` | Deleting a user account cleans up their AI token consumption logs. |
| **`OtpCodes`** | `UserId` | `AspNetUsers(Id)` | `CASCADE` | Deleting a user account purges their active/consumed 2FA OTP codes. |
| **`AspNetUserRoles`** | `UserId` / `RoleId` | `AspNetUsers` / `AspNetRoles` | `CASCADE` | Standard Identity join table cascading cleanup. |
| **`AspNetUserClaims`**| `UserId` | `AspNetUsers(Id)` | `CASCADE` | Standard Identity user claims cleanup. |
| **`AspNetUserLogins`**| `UserId` | `AspNetUsers(Id)` | `CASCADE` | Standard Identity external login tokens cleanup. |
| **`AspNetUserTokens`**| `UserId` | `AspNetUsers(Id)` | `CASCADE` | Standard Identity security token cleanup. |
| **`AspNetRoleClaims`**| `RoleId` | `AspNetRoles(Id)` | `CASCADE` | Standard Identity role claims cleanup. |

---

## Key Schema Invariants & Integrity Constraints

1. **Unique Application Constraint**: `UNIQUE(JobId, StudentId)` on `Applications` enforces at the database level that a student can only apply to a specific job once.
2. **Deterministic Status Enums**: `CHECK` constraints on `VerificationStatus` (0–2), `LocationType` (0–2), `ApplicationStatus` (0–4), `InterviewStatus` (0–2), and `AchievedScore` (0–100) maintain relational integrity independently of the application layer.
3. **Structured JSON Validation**: `ISJSON(DynamicJsonData) = 1` on `Resumes` guarantees valid JSON formatting inside SQL Server storage.
4. **Index Locality**: `NEWSEQUENTIALID()` is used for primary keys rather than `NEWID()` to minimize clustered index fragmentation in SQL Server.
