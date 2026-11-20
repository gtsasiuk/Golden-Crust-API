namespace Platform.EventBus.Topology;

public static class MessagingConstants
{
    public static class Exchanges
    {
        public const string CatalogEvents = "catalog-events";
        public const string OrdersEvents = "orders-events";

        public const string CatalogDeadLetter = "catalog-events.dlx";
    }

    public static class Queues
    {
        public const string OrdersCatalogSync = "orders.catalog-sync";
        public const string ReviewsCatalogSync = "reviews.catalog-sync";

        public const string OrdersCatalogSyncDlq = "orders.catalog-sync.dlq";
        public const string ReviewsCatalogSyncDlq = "reviews.catalog-sync.dlq";
    }

    public static class RoutingKeys
    {
        public const string MenuItemPriceChanged = "product.price_changed";
        public const string MenuItemRenamed = "product.renamed";

        public const string ProductEvents = "product.*";

        public const string OrdersDlq = "orders.catalog-sync";
        public const string ReviewsDlq = "reviews.catalog-sync";
    }
}
