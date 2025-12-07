#!/bin/bash
# Simple script to generate a password hash for MobileAICLI authentication
# Usage: ./generate-password-hash.sh <password>

if [ -z "$1" ]; then
    echo "Usage: ./generate-password-hash.sh <password>"
    echo "Example: ./generate-password-hash.sh mySecurePassword123"
    exit 1
fi

# Use the temp hash generator to create a hash
cd MobileAICLI
dotnet run --no-build << CSHARP
using System;
using System.Security.Cryptography;

string password = "$1";
byte[] salt = RandomNumberGenerator.GetBytes(32);
using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000, HashAlgorithmName.SHA256);
byte[] hash = pbkdf2.GetBytes(32);
string hashString = \$"pbkdf2$100000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
Console.WriteLine("Password hash generated:");
Console.WriteLine(hashString);
Console.WriteLine();
Console.WriteLine("Set this as environment variable:");
Console.WriteLine(\$"export MOBILEAICLI_PASSWORD_HASH='{hashString}'");
CSHARP
