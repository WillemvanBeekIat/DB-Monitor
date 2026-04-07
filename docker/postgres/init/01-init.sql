-- PostgreSQL initialization script for DbMonitor
-- This script runs automatically when the container is first created

-- Create extensions if needed
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Grant privileges
GRANT ALL PRIVILEGES ON DATABASE dbmonitor TO dbmonitor;

-- Log initialization
DO $$
BEGIN
    RAISE NOTICE 'DbMonitor database initialized successfully at %', NOW();
END $$;
