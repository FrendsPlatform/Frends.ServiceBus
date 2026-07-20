# Changelog

## [0.8.0]

### Added

- **SendAndWaitForResponse** — A new task that implements the request-response pattern over Azure Service Bus sessions. The task sends a message to a request queue, then waits for a reply on a session-enabled response queue. Both queues can optionally be created automatically. The response is returned with the same fields as the standard Read task.
