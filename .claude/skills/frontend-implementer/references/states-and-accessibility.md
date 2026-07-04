# States and Accessibility Reference

Contents: full definitions of the required data states (Process step 7) and the detailed accessibility implementation checklist (Process step 10) for the frontend-implementer skill.

## Data States

Every data-dependent area must implement all states from the spec:

- **Empty:** Show the empty state message and action from the spec. Never show a blank space.
- **Loading:** Show the skeleton or spinner defined in the spec. Disable interactive elements while loading.
- **Error:** Show the error message and retry action. Never swallow errors silently.
- **Loaded:** Normal populated state.
- **Partial:** Where applicable — loaded data plus a degraded/error indicator.

## Accessibility Checklist

- Apply semantic roles or AutomationProperties to every region.
- Set accessible names on icon-only buttons, images, and non-obvious controls.
- Implement the tab order from the spec.
- Implement focus management: on open, after actions, after close.
- Implement keyboard shortcuts if specified.
- Apply focus trap to modals and overlays.
- For web: verify colour contrast using the token values.
- For web: implement `prefers-reduced-motion` fallback for any animation.
