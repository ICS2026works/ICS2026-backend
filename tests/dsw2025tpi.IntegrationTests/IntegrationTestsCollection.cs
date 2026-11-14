using Dsw2025Tpi.Api;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace dsw2025tpi.IntegrationTests;

[CollectionDefinition("IntegrationTests")]
public class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory<Program>>
{
    
}
