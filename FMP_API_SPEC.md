# Feature Management Platform API Specification

## Overview
This specification outlines the API for a prototype feature management platform. The API provides functionality to create, read, update, delete, and toggle feature flags, as well as managing environments and users.

## Authentication
Authentication is implemented using a simple API key mechanism.

**Headers:**
```
Authorization: ApiKey YOUR_API_KEY
```

## Base URL
```
https://api.featureflag.example/v1
```

## Endpoints

### Feature Flags

#### List Feature Flags
```
GET /flags
```

**Query Parameters:**
- `project_id` (optional): Filter flags by project
- `environment_id` (optional): Filter flags by environment
- `limit` (optional): Number of items to return (default: 20)
- `offset` (optional): Pagination offset (default: 0)

**Response:**
```json
{
  "flags": [
    {
      "id": "flag-123",
      "key": "new-checkout-flow",
      "name": "New Checkout Flow",
      "description": "Enables the new checkout experience",
      "created_at": "2025-04-01T12:00:00Z",
      "updated_at": "2025-04-05T15:30:00Z",
      "state": {
        "dev": true,
        "staging": true,
        "production": false
      },
      "tags": ["checkout", "beta"]
    }
  ],
  "total": 43,
  "limit": 20,
  "offset": 0
}
```

#### Get a Feature Flag
```
GET /flags/{flag_id}
```

**Response:**
```json
{
  "id": "flag-123",
  "key": "new-checkout-flow",
  "name": "New Checkout Flow",
  "description": "Enables the new checkout experience",
  "created_at": "2025-04-01T12:00:00Z",
  "updated_at": "2025-04-05T15:30:00Z",
  "state": {
    "dev": true,
    "staging": true,
    "production": false
  },
  "tags": ["checkout", "beta"],
  "rules": [
    {
      "id": "rule-456",
      "type": "user",
      "attribute": "email",
      "operator": "ends_with",
      "values": ["@company.com"],
      "environment": "staging"
    }
  ]
}
```

#### Create a Feature Flag
```
POST /flags
```

**Request Body:**
```json
{
  "key": "new-checkout-flow",
  "name": "New Checkout Flow",
  "description": "Enables the new checkout experience",
  "state": {
    "dev": true,
    "staging": false,
    "production": false
  },
  "tags": ["checkout", "beta"]
}
```

**Response:**
```json
{
  "id": "flag-123",
  "key": "new-checkout-flow",
  "name": "New Checkout Flow",
  "description": "Enables the new checkout experience",
  "created_at": "2025-04-07T12:00:00Z",
  "updated_at": "2025-04-07T12:00:00Z",
  "state": {
    "dev": true,
    "staging": false,
    "production": false
  },
  "tags": ["checkout", "beta"]
}
```

#### Update a Feature Flag
```
PUT /flags/{flag_id}
```

**Request Body:**
```json
{
  "name": "Updated Checkout Flow",
  "description": "Enables the redesigned checkout experience",
  "tags": ["checkout", "beta", "redesign"]
}
```

**Response:**
```json
{
  "id": "flag-123",
  "key": "new-checkout-flow",
  "name": "Updated Checkout Flow",
  "description": "Enables the redesigned checkout experience",
  "created_at": "2025-04-01T12:00:00Z",
  "updated_at": "2025-04-07T14:30:00Z",
  "state": {
    "dev": true,
    "staging": true,
    "production": false
  },
  "tags": ["checkout", "beta", "redesign"]
}
```

#### Toggle a Feature Flag State
```
PATCH /flags/{flag_id}/state
```

**Request Body:**
```json
{
  "environment": "staging",
  "enabled": true
}
```

**Response:**
```json
{
  "id": "flag-123",
  "key": "new-checkout-flow",
  "state": {
    "dev": true,
    "staging": true,
    "production": false
  },
  "updated_at": "2025-04-07T15:45:00Z"
}
```

#### Delete a Feature Flag
```
DELETE /flags/{flag_id}
```

**Response:**
```
204 No Content
```

### Targeting Rules

#### Add a Targeting Rule
```
POST /flags/{flag_id}/rules
```

**Request Body:**
```json
{
  "type": "user",
  "attribute": "email",
  "operator": "ends_with",
  "values": ["@company.com"],
  "environment": "staging"
}
```

**Response:**
```json
{
  "id": "rule-456",
  "type": "user",
  "attribute": "email",
  "operator": "ends_with",
  "values": ["@company.com"],
  "environment": "staging",
  "created_at": "2025-04-07T16:00:00Z"
}
```

#### Delete a Targeting Rule
```
DELETE /flags/{flag_id}/rules/{rule_id}
```

**Response:**
```
204 No Content
```

### Environments

#### List Environments
```
GET /environments
```

**Response:**
```json
{
  "environments": [
    {
      "id": "env-123",
      "key": "dev",
      "name": "Development",
      "description": "Development environment",
      "created_at": "2025-03-01T12:00:00Z"
    },
    {
      "id": "env-456",
      "key": "staging",
      "name": "Staging",
      "description": "Staging/QA environment",
      "created_at": "2025-03-01T12:00:00Z"
    },
    {
      "id": "env-789",
      "key": "production",
      "name": "Production",
      "description": "Production environment",
      "created_at": "2025-03-01T12:00:00Z"
    }
  ]
}
```

#### Create Environment
```
POST /environments
```

**Request Body:**
```json
{
  "key": "beta",
  "name": "Beta",
  "description": "Beta test environment"
}
```

**Response:**
```json
{
  "id": "env-101",
  "key": "beta",
  "name": "Beta",
  "description": "Beta test environment",
  "created_at": "2025-04-07T17:00:00Z"
}
```

#### Delete Environment
```
DELETE /environments/{environment_id}
```

**Response:**
```
204 No Content
```

### SDK Configuration

#### Get Client SDK Configuration
```
GET /sdk/config?environment={environment_key}
```

**Response:**
```json
{
  "environment": "production",
  "flags": {
    "new-checkout-flow": false,
    "dark-mode": true,
    "recommendation-engine": true
  },
  "updated_at": "2025-04-07T18:00:00Z"
}
```

#### Evaluate Flags for User
```
POST /sdk/evaluate
```

**Request Body:**
```json
{
  "environment": "production",
  "user": {
    "id": "user-123",
    "email": "john@company.com",
    "groups": ["beta-testers"],
    "country": "US"
  }
}
```

**Response:**
```json
{
  "environment": "production",
  "flags": {
    "new-checkout-flow": true,
    "dark-mode": true,
    "recommendation-engine": true
  },
  "evaluated_at": "2025-04-07T18:05:00Z"
}
```

### Analytics

#### Record Flag Exposure
```
POST /analytics/exposure
```

**Request Body:**
```json
{
  "flag_key": "new-checkout-flow",
  "environment": "production",
  "user_id": "user-123",
  "timestamp": "2025-04-07T18:10:00Z",
  "client_id": "web-app"
}
```

**Response:**
```
204 No Content
```

#### Get Flag Usage Stats
```
GET /analytics/flags/{flag_id}/stats?environment={environment_key}&period=7d
```

**Response:**
```json
{
  "flag_id": "flag-123",
  "flag_key": "new-checkout-flow",
  "environment": "production",
  "period": "7d",
  "exposures": {
    "total": 15420,
    "breakdown": {
      "2025-04-01": 2105,
      "2025-04-02": 2211,
      "2025-04-03": 2098,
      "2025-04-04": 2245,
      "2025-04-05": 2301,
      "2025-04-06": 2198,
      "2025-04-07": 2262
    }
  }
}
```

## Error Responses

All endpoints return appropriate HTTP status codes:

- `200 OK`: The request was successful
- `201 Created`: The resource was created successfully
- `204 No Content`: The request was successful, with no response body
- `400 Bad Request`: The request was invalid
- `401 Unauthorized`: API key is missing or invalid
- `403 Forbidden`: The API key doesn't have permission for the requested operation
- `404 Not Found`: The requested resource was not found
- `409 Conflict`: The request conflicts with current state (e.g., duplicate key)
- `429 Too Many Requests`: Rate limit exceeded
- `500 Internal Server Error`: An error occurred on the server

Error response body:

```json
{
  "error": {
    "code": "invalid_request",
    "message": "Flag key must be unique",
    "details": {
      "field": "key",
      "constraint": "unique"
    }
  }
}
```

## Rate Limiting

API requests are limited to 100 requests per minute per API key. Rate limit information is included in the response headers:

```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1617807600
```