# Sample LDAP Setup

This application shows how to connect to a LDAP server using FactoryTalk Optix, this app includes both the LDAP server and the Optix app

## Requirements

- FactoryTalk Optix Studio 1.7.1.x or higher
- A machine with Docker and the compose plugin

## Setup

## 1. Setting up the LDAP server

Use the following Docker Compose to start the LDAP server and some other useful apps

> [!WARNING]
> This compose is meant for testing and development purposes, it is not production ready, do not use it in production environments without proper security hardening and configuration

> [!WARNING]
> Rockwell automation is not affiliated with the maintainers of the images used in this compose, these images were chosen because they are popular and easy to use, but they may have security vulnerabilities or other issues, make sure to review the images and their configurations before using them

This compose includes:

- **openldap**: This container runs the OpenLDAP server, which is the main component of the setup. It handles LDAP requests and manages the LDAP directory.
- **phpldapadmin**: This container runs phpLDAPadmin, a web-based LDAP client. It provides a user-friendly interface to manage the LDAP server, making it easier to create, modify, and delete LDAP entries.
- **openldap-certs**: This container runs a static file server to serve the OpenLDAP certificates. It allows you to download the automatically generated certificates needed for secure communication with the LDAP server.

```yaml
# Certificates: ipaddress: 1081
# LDAP port: 1389
# Admin portal: ipaddress:1080

services:
  openldap:
    image: bitnamilegacy/openldap:2.6.9
    container_name: openldap
    restart: unless-stopped
    hostname: openldap
    ports: 
      - "1389:1389"  # LDAP with SSL port
      - "1636:1636" # LDAPS port
    volumes:
      - certificates:/opt/bitnami/openldap/certs/
      - config:/bitnami/openldap/
    environment:  # See: https://hub.docker.com/r/bitnami/openldap
      # LDAP domain parameters
      - "LDAP_ROOT=dc=example,dc=org"
      - "LDAP_ORGANISATION=example"
      - "LDAP_DOMAIN=example.org"
      # LDAP certificate settings
      - "LDAP_ENABLE_TLS=yes"
      - "LDAP_REQUIRE_TLS=no"
      - "LDAP_TLS_CERT_FILE=/opt/bitnami/openldap/certs/ldapserver-cacerts.pem"
      - "LDAP_TLS_KEY_FILE=/opt/bitnami/openldap/certs/ldapclient-key.pem"
      - "LDAP_TLS_CA_FILE=/opt/bitnami/openldap/certs/ldapserver-cacerts.pem"
      # LDAP Starting users
      - "LDAP_USERS=user01,user02"
      - "LDAP_PASSWORDS=password1,password2"
      - "LDAP_GROUP=readers"
    networks:
      - openldap

  phpldapadmin:
    image: phpldapadmin/phpldapadmin:2.1
    container_name: phpldapadmin
    restart: unless-stopped
    ports:
      - "1080:8080"
    environment:
      - "LDAP_HOST=openldap"
      - "LDAP_PORT=1389"
      - "LDAP_BASE_DN=dc=example,dc=org"
      - "LDAP_TLS=true"
      - "LDAP_TLS_CACERT_FILE=/certs/ldapserver-cacerts.pem"
    volumes:
      - certificates:/certs
    networks:
      - openldap

  openldap-openssl:
    image: nginx:latest
    container_name: openldap-openssl
    # Do not restart this container or it will delete existing certificates
    restart: no
    # Where to store certificates
    volumes:
      - certificates:/tmp/certs/out
    # Generate the certificates
    command: >
      /bin/bash -c "rm -rf /tmp/certs/out/*.*
      && mkdir -p /tmp/certs/out
      && openssl genrsa -out /tmp/certs/ldapclient-key.pem 1024
      && openssl req -nodes -new -keyout /tmp/certs/ldapclient-key.pem -x509 -days 1095 -out /tmp/certs/ldapserver-cacerts.pem -subj "/C=US/ST=NewJersey/L=Gotham/O=Example/OU=Org/CN=example.org"
      && openssl genrsa -out /tmp/certs/ldapserver-key.pem 1024
      && openssl req -new -key /tmp/certs/ldapserver-key.pem -out /tmp/certs/server.csr -subj "/C=US/ST=NewJersey/L=Gotham/O=Example/OU=Org/CN=example.org"
      && openssl x509 -req -days 2000 -in /tmp/certs/server.csr -CA /tmp/certs/ldapserver-cacerts.pem -CAkey /tmp/certs/ldapclient-key.pem -CAcreateserial -out /tmp/certs/ldapserver-cert.pem
      && cp -rpf /tmp/certs/ldapserver-cacerts.pem /tmp/certs/ldapclient-cacerts.pem
      && cp -rpf /tmp/certs/*.pem /tmp/certs/out/
      && chmod 666 /tmp/certs/out/*.*"

  openldap-certs:
    # This is a static web server used to get the
    # OpenLDAP certificates that were generated automatically
    image: halverneus/static-file-server:latest
    container_name: openldap-certs
    volumes:
      # Expose the certificates volume to the web server
      # using read only mode
      - certificates:/web:ro
    ports:
      - "1081:8080"
    # This container is only needed to get the certificate
    # so, we are not going to restart it
    restart: no
    networks:
      - openldap

networks:
  # Not strictly needed
  openldap:
    driver: bridge

volumes:
  # Persist the configuration of the images
  certificates:
  config:
```

## 2. Add a domain resolution to the LDAP

In order to authenticate, the machine needs to be able to contact the AD/LDAP server using DNS, to do this, we either:

- Add a manual entry to the local DNS server
- Add a manual entry to the `hosts` file in Windows
	- Open a text editor with administrator privileges
	- Browse to `C:\Windows\System32\drivers\etc\hosts`
	- Add a new line: `ipaddress example.org` where `ipaddress` is the IP Address of the device running OpenLDAP

Example:

```plaintext
# be placed in the first column followed by the corresponding host name.
# The IP address and the host name should be separated by at least one
# space.
#
# Additionally, comments (such as these) may be inserted on individual
# lines or following the machine name denoted by a '#' symbol.
#
# For example:
#
#      102.54.94.97     rhino.acme.com          # source server
#       38.25.63.10     x.acme.com              # x client host

# Test LDAP
172.19.20.80 example.org
```

## 3. Download the certificate

- Access the files server container at [http://example.org:1081](http://example.org:1081)
	- If the DNS `example.org` is not working, it means the DNS was not properly set in step #2
- Download the `ldapclient-cacerts.pem` file to the development PC (*right click > save link as*)
- Copy the `ldapclient-cacerts.pem` file to `ProjectFiles/PKI/Own/Certs/ldapclient-cacerts.pem` of the FactoryTalk Optix Optix project (replace if needed)

## 4. Logging in to the LDAP server

Start the runtime and try to login to the LDAP server using the following credentials:

- `cn=user01,ou=users,dc=example,dc=org` with `password1`
- `cn=user02,ou=users,dc=example,dc=org` with `password2`

> [!TIP]
> If the login fails with `Error: ... self-sigend certificate` or some other certificate related error, please check:
> - Make sure the certificate was properly downloaded and added to the Optix project
> - Make sure the DNS is properly set so the client can contact the server
> - Restart the `openldap` container to make sure the certificates are properly loaded (not the whole compose, just the `openldap` container)

## Debugging

### Accessing the list of Users

To read the database and get the list of users, you can enter a command line interface in the openldap container and execute: `slapcat -F /opt/bitnami/openldap/etc/slapd.d`

### Accessing PhpLDAPadmin

Login to the PhpLDAPadmin interface at [http://example.org:1080/](http://example.org:1080/) using the credentials `user01` and `password1`, this interface allows you to easily view and edit the LDAP database.

### Investigating FactoryTalk Optix logs

Set the `Module Log Level Configuration` to `Verbose1` on the `Core` module, this will allow you to see detailed logs about the connection process to the LDAP server, these logs can be useful to troubleshoot connection issues or authentication problems.
