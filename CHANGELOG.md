# Changelog

## 1.0.0 — 2026-11-09

- Compact dashboard heading and status cards give deployments and logs more room.
- Removed the repeated status timestamp below the monitoring controls; current status remains in the status bar.
- Kept the scrollable stacked layout with bounded deployment and log panes.
- Grouped Settings into Connection and Monitoring scope, with local storage details in an expandable section.
- Made Save settings the primary action and stacked settings actions in narrow windows.
- Preserved existing deployment, notification, credential storage, and API workflows.

The x64 MSIX is now self-signed and timestamped. The release includes the public certificate and trust/install instructions. Users must explicitly trust this certificate before installation; it is not a publicly trusted publisher certificate.
