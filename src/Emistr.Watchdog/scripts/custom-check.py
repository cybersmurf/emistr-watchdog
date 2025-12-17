#!/usr/bin/env python3
"""
Custom Health Check - Python Health Check Script
Returns 0 if OK, 1 if warning, 2 if critical

This is a template for custom health checks.
"""

import argparse
import json
import os
import sys
from datetime import datetime

def check_health(verbose=False):
    """
    Perform custom health check.
    
    Returns:
        tuple: (exit_code, message)
            - 0: Healthy
            - 1: Warning
            - 2: Critical
    """
    result = {
        "timestamp": datetime.utcnow().isoformat(),
        "status": "OK",
        "checks": []
    }
    
    # Example check 1: Environment variable
    check_mode = os.environ.get("CHECK_MODE", "default")
    result["checks"].append({
        "name": "Environment",
        "value": check_mode,
        "ok": True
    })
    
    # Example check 2: Some custom logic
    # Replace with your actual health check logic
    cpu_ok = True  # Placeholder
    result["checks"].append({
        "name": "CPU Usage",
        "value": "Normal",
        "ok": cpu_ok
    })
    
    # Example check 3: External dependency
    # api_ok = check_external_api()
    api_ok = True  # Placeholder
    result["checks"].append({
        "name": "External API",
        "value": "Reachable",
        "ok": api_ok
    })
    
    # Determine overall status
    failed_checks = [c for c in result["checks"] if not c["ok"]]
    warning_count = len([c for c in failed_checks if c.get("severity") != "critical"])
    critical_count = len([c for c in failed_checks if c.get("severity") == "critical"])
    
    if critical_count > 0:
        result["status"] = "CRITICAL"
        return (2, result)
    elif warning_count > 0:
        result["status"] = "WARNING"
        return (1, result)
    else:
        result["status"] = "OK"
        return (0, result)


def main():
    parser = argparse.ArgumentParser(description="Custom Health Check Script")
    parser.add_argument("--verbose", "-v", action="store_true", help="Verbose output")
    parser.add_argument("--json", "-j", action="store_true", help="JSON output")
    args = parser.parse_args()
    
    exit_code, result = check_health(verbose=args.verbose)
    
    if args.json:
        print(json.dumps(result, indent=2 if args.verbose else None))
    else:
        print(f"Status: {result['status']}")
        for check in result["checks"]:
            status = "✓" if check["ok"] else "✗"
            print(f"  {status} {check['name']}: {check['value']}")
    
    sys.exit(exit_code)


if __name__ == "__main__":
    main()

