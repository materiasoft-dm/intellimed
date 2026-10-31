import { getOr, getOrNull, nb, ok, qs, sendFor } from './http';
import { dateParam } from '@/lib/dates';
import type {
  AddressBookContactDto, AddressBookSearchDto, ClientAddressDto, ClientAllergyDto, ClientCompensationClaimDto,
  ClientDto, ClientFamilyRelationshipDto, ClientHealthIssueDto, ClientOccupationDto, ClientReferralDto, ClientSearchDto,
  ClientUserDefinedFieldValueDto, CreateClientAddressDto, CreateClientAllergyDto, CreateClientCompensationClaimDto,
  CreateClientDto, CreateClientFamilyRelationshipDto, CreateClientHealthIssueDto, CreateClientOccupationDto,
  CreateClientReferralDto, CreateClientUserDefinedFieldValueDto, CreateEpisodeDto, EpisodeDto, HealthFundDto,
  PagedResult, ProviderGroupDto, SaveAddressBookContactDto, UpdateClientAddressDto, UpdateClientAllergyDto,
  UpdateClientCompensationClaimDto, UpdateClientDto, UpdateClientHealthIssueDto, UpdateClientOccupationDto,
  UpdateClientReferralDto, UpdateClientUserDefinedFieldValueDto, UpdateEpisodeDto, UserDefinedFieldTypeDto,
} from './types.gen';

export const emptyPage = <T>(): PagedResult<T> =>
  ({ items: [], totalCount: 0, page: 1, pageSize: 0, totalPages: 0, hasNextPage: false, hasPreviousPage: false }) as PagedResult<T>;

type IdResult = { id: number };

export const ClientApi = {
  search(search: Partial<ClientSearchDto>): Promise<PagedResult<ClientDto>> {
    const s = search;
    const query = qs({
      page: s.page ?? 1,
      pageSize: s.pageSize ?? 25,
      includeArchived: s.includeArchived ?? false,
      query: nb(s.query),
      isActive: s.isActive,
      surname: nb(s.surname),
      givenName: nb(s.givenName),
      medicareNumber: nb(s.medicareNumber),
      gender: s.gender,
      dvaNumber: nb(s.dvaNumber),
      fileNumber: nb(s.fileNumber),
      pensionNumber: nb(s.pensionNumber),
      healthFundNumber: nb(s.healthFundNumber),
      lifeCardNum: nb(s.lifeCardNum),
      dobFrom: dateParam(s.dobFrom),
      dobTo: dateParam(s.dobTo),
      address: nb(s.address),
      suburb: nb(s.suburb),
      postcode: nb(s.postcode),
      state: nb(s.state),
      postalAddress: nb(s.postalAddress),
      postalSuburb: nb(s.postalSuburb),
      postalPostcode: nb(s.postalPostcode),
      postalState: nb(s.postalState),
      homePhone: nb(s.homePhone),
      businessHoursPhone: nb(s.businessHoursPhone),
      mobilePhone: nb(s.mobilePhone),
      email: nb(s.email),
      atsiStatus: s.atsiStatus,
      createdFrom: dateParam(s.createdFrom),
      createdTo: dateParam(s.createdTo),
      medicareExpiryFrom: dateParam(s.medicareExpiryFrom),
      medicareExpiryTo: dateParam(s.medicareExpiryTo),
      healthFundJoinFrom: dateParam(s.healthFundJoinFrom),
      healthFundJoinTo: dateParam(s.healthFundJoinTo),
      warnings: nb(s.warnings),
      notes: nb(s.notes),
      referredBy: nb(s.referredBy),
      clientType: s.clientType,
      urNumber: nb(s.urNumber),
      healthFundId: s.healthFundId,
      payerClientId: s.payerClientId,
      accountTypes: s.accountTypes ?? undefined,
      deceased: s.deceased,
      acceptEmail: s.acceptEmail,
      acceptSms: s.acceptSms,
      acceptSmsMarketing: s.acceptSmsMarketing,
    });
    return getOr(`api/clients/search${query}`, emptyPage<ClientDto>());
  },
  async create(dto: CreateClientDto): Promise<number | null> {
    return (await sendFor<IdResult>('POST', 'api/clients', dto))?.id ?? null;
  },
  getById: (id: number) => getOrNull<ClientDto>(`api/clients/${id}`),
  update: (id: number, dto: UpdateClientDto) => ok('PUT', `api/clients/${id}`, dto),
  archive: (id: number) => ok('DELETE', `api/clients/${id}`),
};

/** The per-client sub-resources: /api/clients/{clientId}/<segment>. */
function subResource<TItem, TCreate extends { clientId: number }, TUpdate>(segment: string, archiveVerb: 'archive-post' | 'delete' | 'none') {
  return {
    list: (clientId: number) => getOr<TItem[]>(`api/clients/${clientId}/${segment}`, []),
    create: (dto: TCreate) => ok('POST', `api/clients/${dto.clientId}/${segment}`, dto),
    update: (clientId: number, id: number, dto: TUpdate) => ok('PUT', `api/clients/${clientId}/${segment}/${id}`, dto),
    archive: (clientId: number, id: number) =>
      archiveVerb === 'archive-post'
        ? ok('POST', `api/clients/${clientId}/${segment}/${id}/archive`)
        : ok('DELETE', `api/clients/${clientId}/${segment}/${id}`),
    remove: (clientId: number, id: number) => ok('DELETE', `api/clients/${clientId}/${segment}/${id}`),
  };
}

export const ClientAddressApi = subResource<ClientAddressDto, CreateClientAddressDto, UpdateClientAddressDto>('addresses', 'none');
export const ClientAllergyApi = subResource<ClientAllergyDto, CreateClientAllergyDto, UpdateClientAllergyDto>('allergies', 'archive-post');
export const ClientReferralApi = subResource<ClientReferralDto, CreateClientReferralDto, UpdateClientReferralDto>('referrals', 'archive-post');
// WC/TAC claims and occupations archive with DELETE (soft delete server-side)
export const ClientClaimApi = subResource<ClientCompensationClaimDto, CreateClientCompensationClaimDto, UpdateClientCompensationClaimDto>('compensation-claims', 'delete');
export const ClientOccupationApi = subResource<ClientOccupationDto, CreateClientOccupationDto, UpdateClientOccupationDto>('occupations', 'delete');
export const ClientFamilyApi = subResource<ClientFamilyRelationshipDto, CreateClientFamilyRelationshipDto, never>('family', 'none');
export const ClientUdfValueApi = subResource<ClientUserDefinedFieldValueDto, CreateClientUserDefinedFieldValueDto, UpdateClientUserDefinedFieldValueDto>('udf-values', 'none');

export const ClientHealthIssueApi = {
  list: (clientId: number, includeArchived = false) =>
    getOr<ClientHealthIssueDto[]>(`api/clients/${clientId}/health-issues${qs({ includeArchived })}`, []),
  summary: (clientId: number) => getOr<ClientHealthIssueDto[]>(`api/clients/${clientId}/health-issues/summary`, []),
  create: (dto: CreateClientHealthIssueDto) => ok('POST', `api/clients/${dto.clientId}/health-issues`, dto),
  update: (clientId: number, id: number, dto: UpdateClientHealthIssueDto) => ok('PUT', `api/clients/${clientId}/health-issues/${id}`, dto),
  archive: (clientId: number, id: number) => ok('POST', `api/clients/${clientId}/health-issues/${id}/archive`),
};

export const EpisodeApi = {
  list: (clientId: number, includeArchived = false) =>
    getOr<EpisodeDto[]>(`api/clients/${clientId}/episodes${qs({ includeArchived })}`, []),
  async create(dto: CreateEpisodeDto): Promise<number | null> {
    return (await sendFor<IdResult>('POST', `api/clients/${dto.clientId}/episodes`, dto))?.id ?? null;
  },
  update: (clientId: number, id: number, dto: UpdateEpisodeDto) => ok('PUT', `api/clients/${clientId}/episodes/${id}`, dto),
  resolve: (clientId: number, id: number) => ok('POST', `api/clients/${clientId}/episodes/${id}/resolve`),
  reopen: (clientId: number, id: number) => ok('POST', `api/clients/${clientId}/episodes/${id}/reopen`),
  archive: (clientId: number, id: number) => ok('POST', `api/clients/${clientId}/episodes/${id}/archive`),
};

export const AddressBookApi = {
  search(search: Partial<AddressBookSearchDto>): Promise<PagedResult<AddressBookContactDto>> {
    const query = qs({
      page: search.page ?? 1,
      pageSize: search.pageSize ?? 25,
      query: nb(search.query),
      includeArchived: search.includeArchived ? true : undefined,
    });
    return getOr(`api/address-book${query}`, emptyPage<AddressBookContactDto>());
  },
  getById: (id: number) => getOrNull<AddressBookContactDto>(`api/address-book/${id}`),
  async create(dto: SaveAddressBookContactDto): Promise<number | null> {
    return (await sendFor<IdResult>('POST', 'api/address-book', dto))?.id ?? null;
  },
  update: (id: number, dto: SaveAddressBookContactDto) => ok('PUT', `api/address-book/${id}`, dto),
  archive: (id: number) => ok('POST', `api/address-book/${id}/archive`),
};

export const LookupApi = {
  healthFunds: () => getOr<HealthFundDto[]>('api/health-funds', []),
  providerGroups: () => getOr<ProviderGroupDto[]>('api/provider-groups', []),
  udfDefinitions: () => getOr<UserDefinedFieldTypeDto[]>('api/udf-definitions', []),
};
