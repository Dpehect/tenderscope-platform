# Architecture

TenderScope uses a modular monolith with isolated domain, application and infrastructure layers. The API serves query and administration endpoints, while the worker performs scheduled ingestion. Source adapters translate public formats into the canonical tender model. PostgreSQL is the system of record.

The crawler core is deliberately provider-neutral and does not require paid APIs or API keys.
