import { getJson, getOr, getOrNull, ok, qs, sendFor } from './http';
import { emptyPage } from './clients';
import { dateParam } from '@/lib/dates';
import type {
  AccountTypeFeeScheduleMappingDto, BillingItemDto, BillingItemSyncResultDto, CancelInvoiceDto,
  CreateDerivedItemConfigDto, CreateFeeScheduleDto, CreateFeeScheduleItemDto, CreateInvoiceDto, CreatePaymentDto,
  CreateReceiptDto, CreateRefundDto, CreateWriteOffDto, DerivedItemConfigDto, FeeScheduleDto, FeeScheduleFetchResultDto,
  FeeScheduleItemDto, FeeScheduleItemHistoryDto, FeeScheduleItemsPageDto, FeeScheduleSearchDto,
  ImportFeeScheduleItemsRequest, InvoiceDto, InvoiceSearchDto, OutstandingInvoiceDto, PagedResult, PaymentDto,
  PaymentSearchDto, ReceiptDto, ResolveLineRequest, ResolveLineResult, SaveAccountTypeFeeScheduleMappingDto,
  SaveDerivedItemRateCalculatedDto, SeedBulkBillResultDto, SplitInvoiceDto, SplitInvoiceResultDto,
  UpdateDerivedItemConfigDto, UpdateFeeScheduleDto, UpdateFeeScheduleItemOverridesDto,
} from './types.gen';

type IdResult = { id: number };

export const InvoiceApi = {
  search(search: Partial<InvoiceSearchDto>): Promise<PagedResult<InvoiceDto>> {
    const query = qs({
      page: search.page ?? 1,
      pageSize: search.pageSize ?? 25,
      clientId: search.clientId,
      status: search.status,
      fromDate: dateParam(search.fromDate),
      toDate: dateParam(search.toDate),
    });
    return getOr(`api/invoices/search${query}`, emptyPage<InvoiceDto>());
  },
  getById: (id: number) => getOrNull<InvoiceDto>(`api/invoices/${id}`),
  async create(dto: CreateInvoiceDto): Promise<number | null> {
    return (await sendFor<IdResult>('POST', 'api/invoices', dto))?.id ?? null;
  },
  addPayment: (invoiceId: number, dto: CreatePaymentDto) => ok('POST', `api/invoices/${invoiceId}/payments`, dto),
  resolveLine: (request: ResolveLineRequest) => sendFor<ResolveLineResult>('POST', 'api/invoices/resolve-line', request),
  feeScheduleMappings: () => getOr<AccountTypeFeeScheduleMappingDto[]>('api/invoices/fee-schedule-mappings', []),
  saveFeeScheduleMapping: (dto: SaveAccountTypeFeeScheduleMappingDto) => ok('PUT', 'api/invoices/fee-schedule-mappings', dto),
  payments(search: Partial<PaymentSearchDto>): Promise<PagedResult<PaymentDto>> {
    const query = qs({
      page: search.page ?? 1,
      pageSize: search.pageSize ?? 25,
      method: search.method,
      fromDate: dateParam(search.fromDate),
      toDate: dateParam(search.toDate),
    });
    return getOr(`api/invoices/payments${query}`, emptyPage<PaymentDto>());
  },
  writeOff: (invoiceId: number, dto: CreateWriteOffDto) => ok('POST', `api/invoices/${invoiceId}/write-off`, dto),
  cancel: (invoiceId: number, dto: CancelInvoiceDto) => ok('POST', `api/invoices/${invoiceId}/cancel`, dto),
  split: (invoiceId: number, dto: SplitInvoiceDto) => sendFor<SplitInvoiceResultDto>('POST', `api/invoices/${invoiceId}/split`, dto),
};

export const ReceiptApi = {
  getById: (id: number) => getOrNull<ReceiptDto>(`api/receipts/${id}`),
  async create(dto: CreateReceiptDto): Promise<number | null> {
    return (await sendFor<IdResult>('POST', 'api/receipts', dto))?.id ?? null;
  },
  outstandingForPayer: (payerClientId: number, excludeInvoiceId?: number | null) =>
    getOr<OutstandingInvoiceDto[]>(`api/receipts/outstanding${qs({ payerClientId, excludeInvoiceId })}`, []),
  async refund(dto: CreateRefundDto): Promise<number | null> {
    return (await sendFor<IdResult>('POST', 'api/receipts/refund', dto))?.id ?? null;
  },
  async availableCredit(payerClientId: number): Promise<number> {
    try {
      return (await getJson<number>(`api/receipts/available-credit${qs({ payerClientId })}`)) ?? 0;
    } catch {
      return 0;
    }
  },
};

export const FeeScheduleApi = {
  search(search: Partial<FeeScheduleSearchDto>): Promise<PagedResult<FeeScheduleDto>> {
    const query = qs({
      page: search.page ?? 1,
      pageSize: search.pageSize ?? 25,
      includeArchived: search.includeArchived ?? false,
      code: search.code && search.code.trim() ? search.code : undefined,
      description: search.description && search.description.trim() ? search.description : undefined,
    });
    return getOr(`api/fee-schedules/search${query}`, emptyPage<FeeScheduleDto>());
  },
  active: () => getOr<FeeScheduleDto[]>('api/fee-schedules/active', []),
  getById: (id: number) => getOrNull<FeeScheduleDto>(`api/fee-schedules/${id}`),
  async create(dto: CreateFeeScheduleDto): Promise<number | null> {
    return (await sendFor<IdResult>('POST', 'api/fee-schedules', dto))?.id ?? null;
  },
  update: (id: number, dto: UpdateFeeScheduleDto) => ok('PUT', `api/fee-schedules/${id}`, dto),
  archive: (id: number) => ok('POST', `api/fee-schedules/${id}/archive`),
  remove: (id: number) => ok('DELETE', `api/fee-schedules/${id}`),
  items: (feeScheduleId: number) => getOr<FeeScheduleItemDto[]>(`api/fee-schedules/${feeScheduleId}/items`, []),
  itemsPaged: (feeScheduleId: number, skip: number, take: number) =>
    getOr<FeeScheduleItemsPageDto>(`api/fee-schedules/${feeScheduleId}/items/paged${qs({ skip, take })}`,
      { items: [], totalCount: 0 } as unknown as FeeScheduleItemsPageDto),
  getItem: (itemId: number) => getOrNull<FeeScheduleItemDto>(`api/fee-schedules/items/${itemId}`),
  addItem: (feeScheduleId: number, dto: CreateFeeScheduleItemDto) => ok('POST', `api/fee-schedules/${feeScheduleId}/items`, dto),
  updateItemFee: (itemId: number, fee: number) => ok('PUT', `api/fee-schedules/items/${itemId}`, { fee }),
  removeItem: (itemId: number) => ok('DELETE', `api/fee-schedules/items/${itemId}`),
  updateItemOverrides: (itemId: number, dto: UpdateFeeScheduleItemOverridesDto) => ok('PUT', `api/fee-schedules/items/${itemId}/overrides`, dto),
  addPrecalculatedRate: (itemId: number, dto: SaveDerivedItemRateCalculatedDto) => ok('POST', `api/fee-schedules/items/${itemId}/rates`, dto),
  removePrecalculatedRate: (id: number) => ok('DELETE', `api/fee-schedules/rates/${id}`),
  copyFromMbs: (feeScheduleId: number) => sendFor<number>('POST', `api/fee-schedules/${feeScheduleId}/items/copy-from-mbs`),
  importItems: (feeScheduleId: number, request: ImportFeeScheduleItemsRequest) =>
    sendFor<number>('POST', `api/fee-schedules/${feeScheduleId}/items/import`, request),
  seedBulkBill: () => sendFor<SeedBulkBillResultDto>('POST', 'api/fee-schedules/seed-bulk-bill'),
  seedDva: () => sendFor<SeedBulkBillResultDto>('POST', 'api/fee-schedules/seed-dva'),
  seedWorkCoverQld: () => sendFor<SeedBulkBillResultDto>('POST', 'api/fee-schedules/seed-workcover-qld'),
  fetchNow: (feeScheduleId: number) => sendFor<FeeScheduleFetchResultDto>('POST', `api/fee-schedules/${feeScheduleId}/fetch-now`),
  itemHistory: (itemId: number) => getOr<FeeScheduleItemHistoryDto[]>(`api/fee-schedules/items/${itemId}/history`, []),
};

export const BillingItemApi = {
  search: (query: string) => getOr<BillingItemDto[]>(`api/billing-items/search${qs({ query: query ?? '' })}`, []),
  sync: () => sendFor<BillingItemSyncResultDto>('POST', 'api/billing-items/sync'),
};

export const DerivedItemConfigApi = {
  list: () => getOr<DerivedItemConfigDto[]>('api/derived-item-configs', []),
  getById: (id: number) => getOrNull<DerivedItemConfigDto>(`api/derived-item-configs/${id}`),
  async create(dto: CreateDerivedItemConfigDto): Promise<number | null> {
    return (await sendFor<IdResult>('POST', 'api/derived-item-configs', dto))?.id ?? null;
  },
  update: (id: number, dto: UpdateDerivedItemConfigDto) => ok('PUT', `api/derived-item-configs/${id}`, dto),
  remove: (id: number) => ok('DELETE', `api/derived-item-configs/${id}`),
};
