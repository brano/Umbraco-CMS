import type { UmbExecuteTemplateQueryArgs } from './types.js';
import type { TemplateQueryExecuteModel } from '@umbraco-cms/backoffice/backend-api';
import { TemplateResource } from '@umbraco-cms/backoffice/backend-api';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { tryExecuteAndNotify } from '@umbraco-cms/backoffice/resources';

/**
 * A data source for the Template Query Builder that fetches data from the server
 * @export
 * @class UmbTemplateQueryServerDataSource
 */
export class UmbTemplateQueryServerDataSource {
	#host: UmbControllerHost;

	// TODO: When we map the server models to our own models, we need to have a localization property.
	// For example, the OperatorModel.NOT_EQUALS need to use the localization key "template_doesNotEqual"

	/**
	 * Creates an instance of UmbTemplateQueryServerDataSource.
	 * @param {UmbControllerHost} host
	 * @memberof UmbTemplateQueryServerDataSource
	 */
	constructor(host: UmbControllerHost) {
		this.#host = host;
	}
	/**
	 * Fetches the query builder settings from the server
	 *
	 * @return {*}
	 * @memberof UmbTemplateQueryServerDataSource
	 */
	async getTemplateQuerySettings() {
		return tryExecuteAndNotify(this.#host, TemplateResource.getTemplateQuerySettings());
	}
	/**
	 * Executes a query builder query on the server
	 * @param {UmbExecuteTemplateQueryArgs} args
	 * @return {*}
	 * @memberof UmbTemplateQueryServerDataSource
	 */
	async executeTemplateQuery(args: UmbExecuteTemplateQueryArgs) {
		const requestBody: TemplateQueryExecuteModel = {
			rootDocument: args.rootDocument ? { id: args.rootDocument.unique } : null,
			documentTypeAlias: args.documentTypeAlias,
			filters: args.filters,
			sort: args.sort,
			take: args.take,
		};

		return tryExecuteAndNotify(this.#host, TemplateResource.postTemplateQueryExecute({ requestBody }));
	}
}
