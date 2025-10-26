// This service has been removed as part of YAGNI cleanup (T149)
// The NotificationIntegrationService added unnecessary indirection between services
// Individual services (GeofenceService, TeamLocationService, CheckInStorageService)
// already provide event subscriptions that consumers can use directly
//
// Deleted: 2025-10-26
// Reason: Unnecessary abstraction layer - violates YAGNI principle