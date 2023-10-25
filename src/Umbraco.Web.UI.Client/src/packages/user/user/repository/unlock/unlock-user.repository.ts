import { UmbUserRepositoryBase } from '../user-repository-base.js';
import { UmbUnlockUserServerDataSource } from './unlock-user.server.data.js';
import { type UmbControllerHostElement } from '@umbraco-cms/backoffice/controller-api';
import { UserStateModel } from '@umbraco-cms/backoffice/backend-api';

export class UmbUnlockUserRepository extends UmbUserRepositoryBase {
	#source: UmbUnlockUserServerDataSource;

	constructor(host: UmbControllerHostElement) {
		super(host);
		this.#source = new UmbUnlockUserServerDataSource(this.host);
	}

	async unlock(ids: Array<string>) {
		if (ids.length === 0) throw new Error('User ids are missing');
		await this.init;

		const { data, error } = await this.#source.unlock(ids);

		if (!error) {
			ids.forEach((id) => {
				this.detailStore?.updateItem(id, { state: UserStateModel.ACTIVE, failedPasswordAttempts: 0 });
			});

			const notification = { data: { message: `User unlocked` } };
			this.notificationContext?.peek('positive', notification);
		}

		return { data, error };
	}
}
