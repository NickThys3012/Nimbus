export class NavItem {
  label: string;
  icon: string;
  route: string;
  requiresAuth: boolean;
  requiresRole: string | null;

  constructor(
    label: string,
    icon: string,
    route: string,
    requiresAuth: boolean,
    requiresRole: string | null = null,
  ) {
    this.label = label;
    this.icon = icon;
    this.route = route;
    this.requiresAuth = requiresAuth;
    this.requiresRole = requiresRole;
  }
}
