using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;

namespace Cumbot
{
    class RequireRoleAttribute : PreconditionAttribute
    {
        public string role;

        public RequireRoleAttribute(string role)
        {
            this.role = role;
        }

        private ulong GetRoleIdByName(ICommandContext context)
        {
            return context.Guild.Roles
                .Where(x => x.Name.Equals(this.role))
                .ElementAt(0).Id;
        }

        public async override Task<PreconditionResult> CheckPermissions
            (ICommandContext context, CommandInfo command, IServiceProvider map)
        {
            if (((Discord.IGuildUser)context.User).RoleIds.Contains(this.GetRoleIdByName(context)))
                return PreconditionResult.FromSuccess();
            else
                return PreconditionResult.FromError("Insufficient role");
        }
    }
}
