# DDS Study Pass - Product Strategy (2026-03-09)

## Executive Decision

The public DDS Study Pass surface will remain **information-first** for now.

This means:

- public portal: yes
- updates and downloads: yes
- screenshots and product communication: yes
- real course delivery: not yet
- course purchase or unlock flow: not yet
- app-to-course production integration: not yet

This decision is intentional.

The current priority is to keep the public product stable, understandable, and low-risk while the actual course model is still undefined.

## 1. Portal Publico Agora

### Goal

Use the portal as the public face of the app, not as a real course marketplace yet.

### What the portal should expose now

- app presentation
- release notes
- downloads
- screenshots
- roadmap/public updates
- support and help links
- DDS Study Pass positioning as a future expansion area

### What the portal should NOT expose now

- downloadable course payloads
- real course purchase flow
- real unlock flow
- account-bound content
- unfinished course schemas
- unstable backend contracts

### Public navigation recommendation

The portal should be organized around:

- Home
- Downloads
- Updates
- Screenshots
- Roadmap
- Support
- DDS Study Pass

The `DDS Study Pass` page should behave as a preview/status page for now:

- planned modules
- future study areas
- examples of what will exist later
- "coming soon" communication

### Acceptance criteria

- the public portal communicates the product clearly
- downloads remain easy to find
- no unfinished course pipeline is exposed
- the app can safely point users to the portal without creating false expectations

## 2. Course Engine Depois

### Goal

Define the technical model of a course before exposing courses publicly.

### Decisions that still need to be made

#### 2.1 Course packaging

We still need to decide how a course is represented:

- HTML package
- JSON + assets
- ZIP module
- DLC package
- hybrid online/offline module

#### 2.2 Runtime model

We still need to define how the app consumes the course:

- fully local
- fully remote
- hybrid cache

#### 2.3 Progress model

We still need to define how the app tracks learning state:

- lesson progress
- checkpoints
- notes
- favorites
- certificates
- completion

#### 2.4 Authoring workflow

We still need a repeatable way to create courses:

- manual authoring
- structured templates
- content validation
- packaging script
- versioning

### Recommended first technical deliverables

1. define a `course-manifest.json` schema
2. define folder structure for a course module
3. define an internal course player contract
4. define how a course becomes a DLC/module
5. define authoring templates and validation scripts

### Acceptance criteria

- a course can be described without ambiguity
- a course package can be validated before shipping
- the app can consume the package consistently
- the model supports future categories such as `Tecnologia`, `Musica`, and others

## 3. Integracao Futura App + Cursos

### Goal

Only after the course engine exists should the app and portal/server be integrated for real course delivery.

### Future integration layers

#### 3.1 Public catalog layer

- public course listing
- category and module metadata
- screenshots and descriptions
- availability flags

#### 3.2 App handoff layer

- deep links from web to app
- item-specific opening
- module-specific routes
- safe source validation

#### 3.3 Delivery layer

- download or enable course package
- verify package integrity
- install/apply module
- rollback support

#### 3.4 User/account layer

This should come later, not now:

- login
- entitlement
- ownership
- sync
- subscription or purchase model

### Recommended integration order

1. informational catalog
2. course engine
3. app deep links to item context
4. package delivery
5. account/ownership layer

### Acceptance criteria

- the web side can identify an item safely
- the app can open the correct destination
- the course package can be installed or enabled predictably
- account logic remains optional until the base flow is stable

## Product Positioning Summary

### Now

`DDS Study Pass` is a branded preview of the future expansion area.

### Next

The team defines the course engine and packaging model.

### Later

The app and portal gain real course delivery and module interaction.

## Practical Rule For The Current Cycle

For the current cycle:

- public web = app information and product communication
- app = stable platform and internal foundation
- courses = design and architecture phase only

That keeps the DDS StudyOS rollout safe while preserving the path to a real learning platform.

