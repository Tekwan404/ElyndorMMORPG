import {
  adminRequest,
  AdminApiError,
} from '../api'

export const apiClient = {
  request: adminRequest,
}

export { AdminApiError as ApiRequestError }
