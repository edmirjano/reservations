-- Database initialization script
-- This script runs when the PostgreSQL container starts for the first time

-- Create database if it doesn't exist (handled by POSTGRES_DB environment variable)
-- Create extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

-- Set timezone
SET timezone = 'UTC';

-- Create indexes for better performance (will be created by EF Core migrations)
-- These are just examples of what could be added

-- Grant permissions
GRANT ALL PRIVILEGES ON DATABASE reservation TO reservation;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO reservation;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO reservation;

-- Log successful initialization
DO $$
BEGIN
    RAISE NOTICE 'Database initialization completed successfully';
END $$;
