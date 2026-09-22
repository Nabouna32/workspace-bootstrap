# Version policy

Workspace Bootstrap does not impose one global version for every component.

Each component declares its own version semantics and source policy.

| Category | Default policy |
|---|---|
| Windows tooling | Latest stable compatible release |
| Visual Studio / Build Tools | Latest supported release declared by the component |
| .NET | Version pinned by the repository SDK policy and component requirements |
| Node.js | Current LTS compatible with the component |
| Python | Current supported stable release where required |
| Java | LTS compatible with dependent tooling |
| Developer applications | Latest stable unless a profile pins a version |

An exact version is never silently replaced. When the requested version cannot be satisfied, the Engine reports the conflict and leaves the operation recoverable.
