# Implementation Report Template

Contents: full worked example of the frontend-implementer output format — view model interface table, state/interaction/accessibility checklists, tokens table, build result, and implementation code block.

## Implementation: [Screen/Component Name]

**Framework:** [WPF / MAUI / Blazor / React / etc.]
**Spec reference:** [Link or section from ui-designer output]

**Files created or modified:**
- `Views/OrderListView.xaml` — Main view
- `ViewModels/OrderListViewModel.cs` — View model
- `Styles/Tokens.xaml` — Any new tokens added

### View Model Interface

| Property / Command | Type | Direction | Purpose |
|-------------------|------|-----------|---------|
| `Orders` | `ObservableCollection<OrderViewModel>` | One-way | List data |
| `IsLoading` | `bool` | One-way | Loading state |
| `LoadError` | `string?` | One-way | Error message, null when none |
| `SaveCommand` | `ICommand` | Command | Persist changes |
| `SelectedOrder` | `OrderViewModel?` | Two-way | Current selection |

### States Implemented

- [ ] Empty state — message and CTA
- [ ] Loading state — skeleton/spinner, controls disabled
- [ ] Error state — message and retry
- [ ] Loaded state — populated data
- [ ] Partial state (if applicable)

### Interactions Implemented

- [ ] [Trigger] — [Action]
- [ ] [Trigger] — [Action]

### Accessibility

- [ ] Tab order matches spec
- [ ] Focus on open: [element]
- [ ] Focus after [action]: [element]
- [ ] Screen reader labels on icon-only elements
- [ ] Focus trap on modal/overlay (if applicable)
- [ ] Reduced-motion fallback (web, if applicable)

### Tokens Applied

| Token | Value | Applied To |
|-------|-------|------------|
| `color-primary` | `#1A73E8` | Primary button background |
| `space-component` | `16px` | Card padding |

### Build Result

Paste the real output of `dotnet build` / `npm run build`, or an explicit statement of why the project could not be built here.

```
[Build output]
```

```xml
<!-- [Complete implementation code] -->
```
