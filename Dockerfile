# שלב הבנייה - שימוש ב-SDK 9.0
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# העתקת קובץ הפרויקט וביצוע Restore
COPY ["FinalTry.csproj", "."]
RUN dotnet restore "./FinalTry.csproj"

# העתקת שאר הקבצים ובנייה
COPY . .
RUN dotnet publish "FinalTry.csproj" -c Release -o /app/publish /p:UseAppHost=false

# שלב ההרצה
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FinalTry.dll"]
