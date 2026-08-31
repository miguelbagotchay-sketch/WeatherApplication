COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
ENV DOTNET_USE_POLLING_FILE_WATCHER=1

EXPOSE 10000

ENTRYPOINT ["dotnet", "WeatherApplication.dll"]