# Security Policy

## Supported Versions

Currently, only the latest release of SymSmartQueue is actively supported with security updates.

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |


## Reporting a Vulnerability
### Please DO NOT:
- Open a public GitHub issue for security vulnerabilities
- Disclose the vulnerability publicly before it has been addressed

### Please DO:
1. **Report privately** via GitHub Security Advisories:
   - Go to the [Security tab](../../security/advisories)
   - Click "Report a vulnerability"
   - Provide detailed information about the vulnerability

2. **Include in your report:**
   - Description of the vulnerability
   - Steps to reproduce the issue
   - Potential impact
   - Suggested fix (if any)
   - Your contact information

### What to expect:
- **Initial Response**: Within 48 hours
- **Status Update**: Within 7 days with our assessment
- **Fix Timeline**: Depends on severity and complexity
  - Critical: Within 7 days
  - High: Within 30 days
  - Medium: Within 90 days
  - Low: Next regular release

## Security Best Practices for Users

### Plugin Configuration:
1. **Access Control**: Use Jellyfin's built-in user permissions appropriately
2. **HTTPS**: Always access Jellyfin over HTTPS in production
3. **Updates**: Keep Jellyfin server up to date

### Client-Side Security:
- The plugin runs JavaScript in the browser context
- Review custom CSS/JS modifications before applying
- Be cautious with user-generated content

## Contact

For security concerns that don't constitute a vulnerability, you can:
- Open a regular GitHub issue
- Start a discussion in GitHub Discussions
- Contact the maintainers directly

Thank you for helping keep Jellyfin Enhanced secure!