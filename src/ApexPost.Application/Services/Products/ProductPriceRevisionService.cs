using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using ApexPost.Domain.ValueObjects;

namespace ApexPost.Application.Services.Products;

public sealed class ProductPriceRevisionService(
    IProductPriceRevisionRepository repository,
    IUnitOfWork unitOfWork)
{
    public Task<int> SetCategoryPriceAsync(
        string category,
        decimal unitPrice,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);
        if (!Enum.TryParse<ProductCategory>(category, ignoreCase: true, out var parsedCategory) ||
            !Enum.IsDefined(parsedCategory))
        {
            throw new ArgumentException("Select a valid product category.", nameof(category));
        }

        return unitOfWork.ExecuteInTransactionAsync(
            token => repository.SetCategoryPriceAsync(
                parsedCategory, new Money(unitPrice), token),
            cancellationToken);
    }
}
